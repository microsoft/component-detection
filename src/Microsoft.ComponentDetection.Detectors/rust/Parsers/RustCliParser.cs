namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Parser for Cargo.toml files using cargo metadata command.
/// </summary>
public class RustCliParser
{
    private readonly ICommandLineInvocationService cliService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCliParser"/> class.
    /// </summary>
    /// <param name="cliService">The command line invocation service.</param>
    /// <param name="envVarService">The environment variable service.</param>
    /// <param name="logger">The logger.</param>
    public RustCliParser(
        ICommandLineInvocationService cliService,
        IEnvironmentVariableService envVarService,
        ILogger logger)
    {
        this.cliService = cliService;
        this.envVarService = envVarService;
        this.logger = logger;
    }

    /// <summary>
    /// Parses a Cargo.toml file using cargo metadata command.
    /// </summary>
    /// <param name="componentStream">The component stream containing the Cargo.toml file.</param>
    /// <param name="recorder">The component recorder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parse result containing success status and local package directories.</returns>
    public async Task<ParseResult> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        using var record = new RustGraphTelemetryRecord();
        record.CargoTomlLocation = componentStream.Location;

        try
        {
            if (this.IsRustCliManuallyDisabled())
            {
                this.logger.LogInformation("Rust CLI manually disabled for {Location}", componentStream.Location);
                result.FailureReason = "Manually Disabled";
                return result;
            }

            if (!await this.cliService.CanCommandBeLocatedAsync("cargo", null))
            {
                this.logger.LogInformation("Could not locate cargo command for {Location}", componentStream.Location);
                result.FailureReason = "Could not locate cargo command";
                return result;
            }

            var cliResult = await this.cliService.ExecuteCommandAsync(
                command: "cargo",
                additionalCandidateCommands: null,
                workingDirectory: null,
                cancellationToken: cancellationToken,
                "metadata",
                "--manifest-path",
                componentStream.Location,
                "--format-version=1",
                "--locked");

            if (cliResult.ExitCode != 0)
            {
                this.logger.LogWarning("`cargo metadata` failed for {Location}: {Error}", componentStream.Location, cliResult.StdErr);
                result.ErrorMessage = cliResult.StdErr;
                result.FailureReason = "`cargo metadata` failed";
                return result;
            }

            var metadata = CargoMetadata.FromJson(cliResult.StdOut);
            var graph = this.BuildGraph(metadata);

            var packages = metadata.Packages.ToDictionary(
                x => $"{x.Id}",
                x => new CargoComponent(
                    x.Name,
                    x.Version,
                    (x.Authors == null || x.Authors.Any(a => string.IsNullOrWhiteSpace(a)) || x.Authors.Length == 0) ? null : string.Join(", ", x.Authors),
                    string.IsNullOrWhiteSpace(x.License) ? null : x.License,
                    x.Source));

            var root = metadata.Resolve.Root;
            HashSet<string> visitedDependencies = [];

            if (root == null)
            {
                this.logger.LogInformation("Virtual Manifest detected: {Location}", componentStream.Location);
                foreach (var dep in metadata.Resolve.Nodes)
                {
                    var componentKey = $"{dep.Id}";
                    if (visitedDependencies.Add(componentKey))
                    {
                        this.TraverseAndRecordComponents(
                            recorder,
                            componentStream.Location,
                            graph,
                            dep.Id,
                            null,
                            null,
                            packages,
                            visitedDependencies,
                            explicitlyReferencedDependency: false);
                    }
                }
            }
            else
            {
                this.TraverseAndRecordComponents(
                    recorder,
                    componentStream.Location,
                    graph,
                    root,
                    null,
                    null,
                    packages,
                    visitedDependencies,
                    explicitlyReferencedDependency: true,
                    isTomlRoot: true);
            }

            // Collect local package directories
            foreach (var package in metadata.Packages.Where(p => p.Source == null))
            {
                var pkgDir = Path.GetDirectoryName(package.ManifestPath);
                if (!string.IsNullOrEmpty(pkgDir))
                {
                    result.LocalPackageDirectories.Add(pkgDir);
                }
            }

            result.Success = true;
            return result;
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to run cargo metadata for {Location}", componentStream.Location);
            result.ErrorMessage = e.Message;
            result.FailureReason = "Exception during cargo metadata";
            return result;
        }
    }

    private Dictionary<string, Node> BuildGraph(CargoMetadata cargoMetadata) =>
        cargoMetadata.Resolve.Nodes.ToDictionary(x => x.Id);

    private bool IsRustCliManuallyDisabled() =>
        this.envVarService.IsEnvironmentVariableValueTrue("DisableRustCliScan");

    private void TraverseAndRecordComponents(
        ISingleFileComponentRecorder recorder,
        string location,
        IReadOnlyDictionary<string, Node> graph,
        string id,
        DetectedComponent parent,
        Dep depInfo,
        IReadOnlyDictionary<string, CargoComponent> packagesMetadata,
        ISet<string> visitedDependencies,
        bool explicitlyReferencedDependency = false,
        bool isTomlRoot = false)
    {
        try
        {
            var isDevelopmentDependency = depInfo?.DepKinds.Any(x => x.Kind is Kind.Dev) ?? false;

            if (!packagesMetadata.TryGetValue($"{id}", out var cargoComponent))
            {
                this.logger.LogWarning("Did not find dependency '{Id}' in Manifest.packages, skipping", id);
                return;
            }

            var detectedComponent = new DetectedComponent(cargoComponent);

            if (!graph.TryGetValue(id, out var node))
            {
                this.logger.LogWarning("Could not find {Id} at {Location} in cargo metadata output", id, location);
                return;
            }

            var shouldRegister = !isTomlRoot && cargoComponent.Source != null;
            if (shouldRegister)
            {
                recorder.RegisterUsage(
                    detectedComponent,
                    explicitlyReferencedDependency,
                    isDevelopmentDependency: isDevelopmentDependency,
                    parentComponentId: parent?.Component.Id);
            }

            foreach (var dep in node.Deps)
            {
                var componentKey = $"{detectedComponent.Component.Id}{dep.Pkg} {isTomlRoot}";
                if (visitedDependencies.Add(componentKey))
                {
                    this.TraverseAndRecordComponents(
                        recorder,
                        location,
                        graph,
                        dep.Pkg,
                        shouldRegister ? detectedComponent : null,
                        dep,
                        packagesMetadata,
                        visitedDependencies,
                        explicitlyReferencedDependency: isTomlRoot && explicitlyReferencedDependency);
                }
            }
        }
        catch (IndexOutOfRangeException e)
        {
            this.logger.LogWarning(e, "Could not parse {Id} at {Location}", id, location);
            recorder.RegisterPackageParseFailure(id);
        }
    }

    /// <summary>
    /// Result of parsing a Cargo.toml file.
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether parsing was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if parsing failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the reason for failure if parsing failed.
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Gets or sets the local package directories that should be marked as visited.
        /// </summary>
        public HashSet<string> LocalPackageDirectories { get; set; } = [];
    }
}
