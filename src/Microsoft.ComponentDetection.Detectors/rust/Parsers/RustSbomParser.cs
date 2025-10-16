namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Sbom.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detector for Cargo SBOM (.cargo-sbom.json) files.
/// </summary>
public class RustSbomParser
{
    private const string CratesIoSource = "registry+https://github.com/rust-lang/crates.io-index";

    /// <summary>
    /// Cargo Package ID: source#name@version
    /// https://rustwiki.org/en/cargo/reference/pkgid-spec.html.
    /// </summary>
    private static readonly Regex CargoPackageIdRegex = new Regex(
        @"^(?<source>[^#]*)#?(?<name>[\w\-]*)[@#]?(?<version>\d[\S]*)?$",
        RegexOptions.Compiled);

    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustSbomParser"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RustSbomParser(ILogger logger) => this.logger = logger;

    /// <summary>
    /// Parses a Cargo SBOM file and records components.
    /// </summary>
    /// <param name="componentStream">The component stream containing the SBOM file.</param>
    /// <param name="recorder">The component recorder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lockfile version, or null if parsing failed.</returns>
    public async Task<int?> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(componentStream.Stream);
            var cargoSbom = CargoSbom.FromJson(await reader.ReadToEndAsync(cancellationToken));
            this.ProcessCargoSbom(cargoSbom, recorder, componentStream);
            return cargoSbom.Version;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to parse Cargo SBOM file '{FileLocation}'", componentStream.Location);
            return null;
        }
    }

    private void ProcessCargoSbom(CargoSbom sbom, ISingleFileComponentRecorder recorder, IComponentStream components)
    {
        try
        {
            var visitedNodes = new HashSet<int>();
            this.ProcessDependency(sbom, sbom.Crates[sbom.Root], recorder, components, visitedNodes);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to process Cargo SBOM file '{FileLocation}'", components.Location);
        }
    }

    private void ProcessDependency(
        CargoSbom sbom,
        SbomCrate package,
        ISingleFileComponentRecorder recorder,
        IComponentStream components,
        HashSet<int> visitedNodes,
        CargoComponent parent = null,
        int depth = 0)
    {
        foreach (var dependency in package.Dependencies)
        {
            var dep = sbom.Crates[dependency.Index];
            var parentComponent = parent;

            if (this.ParsePackageIdSpec(dep.Id, out var component))
            {
                if (component.Source == CratesIoSource)
                {
                    parentComponent = component;
                    recorder.RegisterUsage(
                        new DetectedComponent(component),
                        isExplicitReferencedDependency: depth == 0,
                        parent?.Id,
                        isDevelopmentDependency: false);
                }
            }
            else
            {
                this.logger.LogError(null, "Failed to parse Cargo PackageIdSpec '{Id}' in '{Location}'", dep.Id, components.Location);
                recorder.RegisterPackageParseFailure(dep.Id);
            }

            if (visitedNodes.Add(dependency.Index))
            {
                this.ProcessDependency(sbom, dep, recorder, components, visitedNodes, parentComponent, depth + 1);
            }
        }
    }

    /// <summary>
    /// Parses a Cargo SBOM file and registers each discovered component against all owning Cargo.toml recorders
    /// using the provided ownership map (cargo metadata package id -> set of manifest paths).
    /// Falls back to the supplied sbomRecorder when ownership info is absent.
    /// </summary>
    /// <param name="componentStream">SBOM stream.</param>
    /// <param name="sbomRecorder">Recorder tied to the SBOM file (fallback target).</param>
    /// <param name="parentComponentRecorder">Root component recorder used to create (or reuse) per-manifest recorders.</param>
    /// <param name="ownershipMap">Package ownership map from RustMetadataContextBuilder (may be null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SBOM version or null on failure.</returns>
    public async Task<int?> ParseWithOwnershipAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder sbomRecorder,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(componentStream.Stream);
            var cargoSbom = CargoSbom.FromJson(await reader.ReadToEndAsync(cancellationToken));
            this.ProcessCargoSbomWithOwnership(
                cargoSbom,
                componentStream,
                sbomRecorder,
                parentComponentRecorder,
                ownershipMap);
            return cargoSbom.Version;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to parse Cargo SBOM (ownership mode) '{FileLocation}'", componentStream.Location);
            return null;
        }
    }

    private void ProcessCargoSbomWithOwnership(
        CargoSbom sbom,
        IComponentStream sbomStream,
        ISingleFileComponentRecorder sbomRecorder,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap)
    {
        try
        {
            var visitedNodes = new HashSet<int>();
            this.ProcessDependencyWithOwnership(
                sbom,
                sbom.Crates[sbom.Root],
                sbomStream,
                sbomRecorder,
                parentComponentRecorder,
                ownershipMap,
                visitedNodes,
                parent: null,
                depth: 0);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to process Cargo SBOM (ownership mode) '{FileLocation}'", sbomStream.Location);
        }
    }

    private void ProcessDependencyWithOwnership(
        CargoSbom sbom,
        SbomCrate package,
        IComponentStream sbomStream,
        ISingleFileComponentRecorder sbomRecorder,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        HashSet<int> visitedNodes,
        CargoComponent parent,
        int depth)
    {
        foreach (var dependency in package.Dependencies)
        {
            var depCrate = sbom.Crates[dependency.Index];
            var parentComponent = parent;

            if (this.ParsePackageIdSpec(depCrate.Id, out var component))
            {
                if (component.Source == CratesIoSource)
                {
                    parentComponent = component;

                    // Determine ownership
                    var metadataId = depCrate.Id;
                    var ownersApplied = false;

                    if (ownershipMap != null &&
                        parentComponentRecorder != null &&
                        ownershipMap.TryGetValue(metadataId, out var owners) &&
                        owners != null && owners.Count > 0)
                    {
                        ownersApplied = true;
                        foreach (var manifestPath in owners)
                        {
                            var ownerRecorder = parentComponentRecorder.CreateSingleFileComponentRecorder(manifestPath);
                            ownerRecorder.RegisterUsage(
                                new DetectedComponent(component),
                                isExplicitReferencedDependency: depth == 0,
                                parentComponentId: null,
                                isDevelopmentDependency: false);
                        }
                    }

                    if (!ownersApplied)
                    {
                        this.logger.LogWarning("Falling back to SBOM recorder for {Id} because no ownership found", metadataId);

                        // Fallback to SBOM recorder if no ownership info
                        sbomRecorder.RegisterUsage(
                            new DetectedComponent(component),
                            isExplicitReferencedDependency: depth == 0,
                            parentComponentId: null,
                            isDevelopmentDependency: false);
                    }
                }
            }
            else
            {
                this.logger.LogError(null, "Failed to parse Cargo PackageIdSpec '{Id}' in '{Location}'", depCrate.Id, sbomStream.Location);
                sbomRecorder.RegisterPackageParseFailure(depCrate.Id);
            }

            if (visitedNodes.Add(dependency.Index))
            {
                this.ProcessDependencyWithOwnership(
                    sbom,
                    depCrate,
                    sbomStream,
                    sbomRecorder,
                    parentComponentRecorder,
                    ownershipMap,
                    visitedNodes,
                    parentComponent,
                    depth + 1);
            }
        }
    }

    private bool ParsePackageIdSpec(string dependency, out CargoComponent component)
    {
        var match = CargoPackageIdRegex.Match(dependency);
        var name = match.Groups["name"].Value;
        var version = match.Groups["version"].Value;
        var source = match.Groups["source"].Value;

        if (!match.Success)
        {
            component = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = source[(source.LastIndexOf('/') + 1)..];
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            source = null;
        }

        component = new CargoComponent(name, version, source: source);
        return true;
    }
}
