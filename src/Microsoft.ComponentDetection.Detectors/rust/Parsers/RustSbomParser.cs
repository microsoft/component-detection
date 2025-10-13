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
    /// Initializes a new instance of the <see cref="RustSbomDetector"/> class.
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
