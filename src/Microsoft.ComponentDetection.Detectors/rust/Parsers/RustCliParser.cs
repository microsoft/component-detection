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
using static Microsoft.ComponentDetection.Detectors.Rust.IRustCliParser;

/// <summary>
/// Parser for Cargo.toml files using cargo metadata command or cached metadata,
/// with optional ownership-aware component registration.
/// </summary>
public class RustCliParser : IRustCliParser
{
    private readonly ICommandLineInvocationService cliService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly ILogger<RustCliParser> logger;
    private readonly IPathUtilityService pathUtilityService;

    public RustCliParser(
        ICommandLineInvocationService cliService,
        IEnvironmentVariableService envVarService,
        IPathUtilityService pathUtilityService,
        ILogger<RustCliParser> logger)
    {
        this.cliService = cliService;
        this.envVarService = envVarService;
        this.pathUtilityService = pathUtilityService;
        this.logger = logger;
    }

    /// <summary>
    /// Parses a Cargo.toml file by invoking 'cargo metadata'.
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
            return this.ProcessMetadata(
                componentStream.Location,
                fallbackRecorder: recorder,
                metadata,
                parentComponentRecorder: null,
                ownershipMap: null);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to run cargo metadata for {Location}", componentStream.Location);
            result.ErrorMessage = e.Message;
            result.FailureReason = "Exception during cargo metadata";
            return result;
        }
    }

    /// <summary>
    /// Parses a Cargo.toml file using a previously obtained CargoMetadata (cached output).
    /// Avoids re-running the cargo command.
    /// </summary>
    /// <returns>Result of parsing cargo metadata.</returns>
    public Task<ParseResult> ParseFromMetadataAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder fallbackRecorder,
        CargoMetadata cachedMetadata,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        if (cachedMetadata == null)
        {
            result.FailureReason = "Cached metadata unavailable";
            return Task.FromResult(result);
        }

        if (this.IsRustCliManuallyDisabled())
        {
            this.logger.LogInformation("Rust CLI manually disabled (cached path) for {Location}", componentStream.Location);
            result.FailureReason = "Manually Disabled";
            return Task.FromResult(result);
        }

        try
        {
            return Task.FromResult(this.ProcessMetadata(
                componentStream.Location,
                fallbackRecorder,
                cachedMetadata,
                parentComponentRecorder,
                ownershipMap));
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed processing cached cargo metadata for {Location}", componentStream.Location);
            result.ErrorMessage = e.Message;
            result.FailureReason = "Exception during cached cargo metadata processing";
            return Task.FromResult(result);
        }
    }

    private ParseResult ProcessMetadata(
        string manifestLocation,
        ISingleFileComponentRecorder fallbackRecorder,
        CargoMetadata metadata,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap)
    {
        var result = new ParseResult();

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
            this.logger.LogInformation("Virtual Manifest detected: {Location}", this.pathUtilityService.NormalizePath(manifestLocation));
            foreach (var dep in metadata.Resolve.Nodes)
            {
                var componentKey = $"{dep.Id}";
                if (visitedDependencies.Add(componentKey))
                {
                    this.TraverseAndRecordComponents(
                        manifestLocation,
                        graph,
                        dep.Id,
                        parent: null,
                        depInfo: null,
                        packagesMetadata: packages,
                        visitedDependencies: visitedDependencies,
                        explicitlyReferencedDependency: false,
                        isTomlRoot: false,
                        parentComponentRecorder: parentComponentRecorder,
                        ownershipMap: ownershipMap,
                        fallbackRecorder: fallbackRecorder);
                }
            }
        }
        else
        {
            this.TraverseAndRecordComponents(
                manifestLocation,
                graph,
                root,
                parent: null,
                depInfo: null,
                packagesMetadata: packages,
                visitedDependencies: visitedDependencies,
                explicitlyReferencedDependency: true,
                isTomlRoot: true,
                parentComponentRecorder: parentComponentRecorder,
                ownershipMap: ownershipMap,
                fallbackRecorder: fallbackRecorder);
        }

        foreach (var package in metadata.Packages.Where(p => p.Source == null))
        {
            var pkgDir = Path.GetDirectoryName(package.ManifestPath);
            if (!string.IsNullOrEmpty(pkgDir))
            {
                result.LocalPackageDirectories.Add(this.pathUtilityService.NormalizePath(pkgDir));
            }
        }

        result.Success = true;
        return result;
    }

    private Dictionary<string, Node> BuildGraph(CargoMetadata cargoMetadata) =>
        cargoMetadata.Resolve.Nodes.ToDictionary(x => x.Id);

    private bool IsRustCliManuallyDisabled() =>
        this.envVarService.IsEnvironmentVariableValueTrue("DisableRustCliScan");

    private void TraverseAndRecordComponents(
        string location,
        IReadOnlyDictionary<string, Node> graph,
        string id,
        DetectedComponent parent,
        Dep depInfo,
        IReadOnlyDictionary<string, CargoComponent> packagesMetadata,
        ISet<string> visitedDependencies,
        bool explicitlyReferencedDependency,
        bool isTomlRoot,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        ISingleFileComponentRecorder fallbackRecorder)
    {
        try
        {
            var isDevelopmentDependency = depInfo?.DepKinds?.Any(x => x.Kind is Kind.Dev) ?? false;

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
                this.ApplyOwners(
                    id,
                    detectedComponent,
                    explicitlyReferencedDependency,
                    isDevelopmentDependency,
                    parentComponentRecorder,
                    ownershipMap,
                    fallbackRecorder,
                    parent);
            }

            foreach (var dep in node.Deps)
            {
                var componentKey = $"{detectedComponent.Component.Id}{dep.Pkg} {isTomlRoot}";
                if (visitedDependencies.Add(componentKey))
                {
                    this.TraverseAndRecordComponents(
                        location,
                        graph,
                        dep.Pkg,
                        shouldRegister ? detectedComponent : null,
                        dep,
                        packagesMetadata,
                        visitedDependencies,
                        explicitlyReferencedDependency: isTomlRoot && explicitlyReferencedDependency,
                        isTomlRoot: false,
                        parentComponentRecorder: parentComponentRecorder,
                        ownershipMap: ownershipMap,
                        fallbackRecorder: fallbackRecorder);
                }
            }
        }
        catch (IndexOutOfRangeException e)
        {
            this.logger.LogWarning(e, "Could not parse {Id} at {Location}", id, location);
            fallbackRecorder.RegisterPackageParseFailure(id);
        }
    }

    private void ApplyOwners(
        string id,
        DetectedComponent detectedComponent,
        bool explicitlyReferencedDependency,
        bool isDevelopmentDependency,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        ISingleFileComponentRecorder fallbackRecorder,
        DetectedComponent parent)
    {
        var ownersApplied = false;
        var parentId = parent?.Component.Id;
        if (ownershipMap != null &&
            parentComponentRecorder != null &&
            ownershipMap.TryGetValue(id, out var owners) &&
            owners != null && owners.Count > 0)
        {
            ownersApplied = true;
            foreach (var manifestPath in owners)
            {
                var ownerRecorder = parentComponentRecorder.CreateSingleFileComponentRecorder(manifestPath);
                ownerRecorder.RegisterUsage(
                    detectedComponent,
                    explicitlyReferencedDependency,
                    isDevelopmentDependency: isDevelopmentDependency,
                    parentComponentId: parentId != null && ownerRecorder.DependencyGraph.Contains(parentId) ? parentId : null);
            }
        }

        if (!ownersApplied)
        {
            // Fallback to the manifest-local recorder
            fallbackRecorder.RegisterUsage(
                detectedComponent,
                explicitlyReferencedDependency,
                isDevelopmentDependency: isDevelopmentDependency,
                parentComponentId: parentId != null && fallbackRecorder.DependencyGraph.Contains(parentId) ? parentId : null);
        }
    }
}
