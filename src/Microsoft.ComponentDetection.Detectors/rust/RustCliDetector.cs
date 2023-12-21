namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Rust CLI detector that uses the cargo metadata command to detect Rust components.
/// </summary>
public class RustCliDetector : FileComponentDetector, IExperimentalDetector
{
    private readonly ICommandLineInvocationService cliService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCliDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The component stream enumerable factory.</param>
    /// <param name="walkerFactory">The walker factory.</param>
    /// <param name="cliService">The command line invocation service.</param>
    /// <param name="logger">The logger.</param>
    public RustCliDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService cliService,
        ILogger<RustCliDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.cliService = cliService;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override string Id => "RustCli";

    /// <inheritdoc />
    public override IEnumerable<string> Categories { get; } = new[] { "Rust" };

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Cargo };

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = new[] { "Cargo.toml" };

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var componentStream = processRequest.ComponentStream;

        try
        {
            if (!await this.cliService.CanCommandBeLocatedAsync("cargo", null))
            {
                this.Logger.LogWarning("Could not locate cargo command. Skipping Rust CLI detection");
                return;
            }

            // Use --all-features to ensure that even optional feature dependencies are detected.
            var cliResult = await this.cliService.ExecuteCommandAsync(
                "cargo",
                null,
                "metadata",
                "--all-features",
                "--manifest-path",
                componentStream.Location,
                "--format-version=1",
                "--locked");

            if (cliResult.ExitCode < 0)
            {
                this.Logger.LogWarning("`cargo metadata` failed with {Location}. Ensure the Cargo.lock is up to date. stderr: {StdErr}", processRequest.ComponentStream.Location, cliResult.StdErr);
                return;
            }

            var metadata = CargoMetadata.FromJson(cliResult.StdOut);
            var graph = BuildGraph(metadata);

            var packages = metadata.Packages.ToDictionary(
                x => $"{x.Name} {x.Version}",
                x => (
                    (x.Authors == null || x.Authors.All(a => string.IsNullOrWhiteSpace(a))) ? null : string.Join(", ", x.Authors),
                    string.IsNullOrWhiteSpace(x.License) ? null : x.License));

            var root = metadata.Resolve.Root;

            this.TraverseAndRecordComponents(processRequest.SingleFileComponentRecorder, componentStream.Location, graph, root, null, null, packages);
        }
        catch (InvalidOperationException e)
        {
            this.Logger.LogWarning(e, "Failed attempting to call `cargo` with file: {Location}", processRequest.ComponentStream.Location);
        }
    }

    private static Dictionary<string, Node> BuildGraph(CargoMetadata cargoMetadata) => cargoMetadata.Resolve.Nodes.ToDictionary(x => x.Id);

    private static (string Name, string Version) ParseNameAndVersion(string nameAndVersion)
    {
        var parts = nameAndVersion.Split(' ');
        return (parts[0], parts[1]);
    }

    private void TraverseAndRecordComponents(
        ISingleFileComponentRecorder recorder,
        string location,
        IReadOnlyDictionary<string, Node> graph,
        string id,
        DetectedComponent parent,
        Dep depInfo,
        IReadOnlyDictionary<string, (string Authors, string License)> packages,
        bool explicitlyReferencedDependency = false)
    {
        try
        {
            var isDevelopmentDependency = depInfo?.DepKinds.Any(x => x.Kind is Kind.Dev) ?? false;
            var (name, version) = ParseNameAndVersion(id);

            var (authors, license) = packages.TryGetValue($"{name} {version}", out var package)
                ? package
                : (null, null);

            var detectedComponent = new DetectedComponent(new CargoComponent(name, version, authors, license));

            recorder.RegisterUsage(
                detectedComponent,
                explicitlyReferencedDependency,
                isDevelopmentDependency: isDevelopmentDependency,
                parentComponentId: parent?.Component.Id);

            if (!graph.TryGetValue(id, out var node))
            {
                this.Logger.LogWarning("Could not find {Id} at {Location} in cargo metadata output", id, location);
                return;
            }

            foreach (var dep in node.Deps)
            {
                this.TraverseAndRecordComponents(recorder, location, graph, dep.Pkg, detectedComponent, dep, packages, parent == null);
            }
        }
        catch (IndexOutOfRangeException e)
        {
            this.Logger.LogWarning(e, "Could not parse {Id} at {Location}", id, location);
            recorder.RegisterPackageParseFailure(id);
        }
    }
}
