#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Sbom.Contracts;
using Microsoft.Extensions.Logging;

public class RustSbomDetector : FileComponentDetector, IExperimentalDetector
{
    private const string CargoSbomSearchPattern = "*.cargo-sbom.json";
    private const string CratesIoSource = "registry+https://github.com/rust-lang/crates.io-index";

    /// <summary>
    /// Cargo Package ID: source#name@version
    /// https://rustwiki.org/en/cargo/reference/pkgid-spec.html.
    /// </summary>
    private static readonly Regex CargoPackageIdRegex = new Regex(
        @"^(?<source>[^#]*)#?(?<name>[\w\-]*)[@#]?(?<version>\d[\S]*)?$",
        RegexOptions.Compiled);

    public RustSbomDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<RustSbomDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "RustSbom";

    public override IList<string> SearchPatterns => [CargoSbomSearchPattern];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Cargo];

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => ["Rust"];

    private static bool ParsePackageIdSpec(string dependency, out CargoComponent component)
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

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var components = processRequest.ComponentStream;
        var reader = new StreamReader(components.Stream);
        var cargoSbom = CargoSbom.FromJson(await reader.ReadToEndAsync(cancellationToken));
        this.RecordLockfileVersion(cargoSbom.Version);
        this.ProcessCargoSbom(cargoSbom, singleFileComponentRecorder, components);
    }

    private void ProcessDependency(CargoSbom sbom, SbomCrate package, ISingleFileComponentRecorder recorder, IComponentStream components, HashSet<int> visitedNodes, CargoComponent parent = null, int depth = 0)
    {
        foreach (var dependency in package.Dependencies)
        {
            var dep = sbom.Crates[dependency.Index];
            var parentComponent = parent;
            if (ParsePackageIdSpec(dep.Id, out var component))
            {
                if (component.Source == CratesIoSource)
                {
                    parentComponent = component;
                    recorder.RegisterUsage(new DetectedComponent(component), isExplicitReferencedDependency: depth == 0, parent?.Id, isDevelopmentDependency: false);
                }
            }
            else
            {
                this.Logger.LogError(null, "Failed to parse Cargo PackageIdSpec '{Id}' in '{Location}'", dep.Id, components.Location);
                recorder.RegisterPackageParseFailure(dep.Id);
            }

            if (visitedNodes.Add(dependency.Index))
            {
                // Skip processing already processed nodes
                this.ProcessDependency(sbom, dep, recorder, components, visitedNodes, parentComponent, depth + 1);
            }
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
            // If something went wrong, just ignore the file
            this.Logger.LogError(e, "Failed to process Cargo SBOM file '{FileLocation}'", components.Location);
        }
    }
}
