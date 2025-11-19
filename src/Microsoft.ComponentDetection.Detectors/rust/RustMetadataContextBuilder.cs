#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;
using static Microsoft.ComponentDetection.Detectors.Rust.IRustMetadataContextBuilder;

public class RustMetadataContextBuilder : IRustMetadataContextBuilder
{
    private readonly ILogger<RustMetadataContextBuilder> logger;
    private readonly ICommandLineInvocationService cliService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly IEnvironmentVariableService envVarService;

    public RustMetadataContextBuilder(
        ILogger<RustMetadataContextBuilder> logger,
        ICommandLineInvocationService cliService,
        IPathUtilityService pathUtilityService,
        IEnvironmentVariableService envVarService)
    {
        this.logger = logger;
        this.cliService = cliService;
        this.pathUtilityService = pathUtilityService;
        this.envVarService = envVarService;
    }

    public async Task<OwnershipResult> BuildPackageOwnershipMapAsync(
        IEnumerable<string> orderedTomlPaths,
        CancellationToken cancellationToken = default)
    {
        var aggregate = new OwnershipResult();

        // Check if Rust CLI scanning is manually disabled
        if (this.IsRustCliManuallyDisabled())
        {
            this.logger.LogInformation("Rust CLI manually disabled, skipping package ownership map build");
            return aggregate;
        }

        var visitedManifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toml in orderedTomlPaths ?? [])
        {
            var normToml = this.pathUtilityService.NormalizePath(toml);
            if (visitedManifests.Contains(normToml))
            {
                this.logger.LogDebug("Skipping {Toml} (already visited)", normToml);
                continue;
            }

            var metadata = await this.RunCargoMetadataAsync(toml, cancellationToken);
            if (metadata == null)
            {
                this.logger.LogWarning("Skipping TOML due to cargo metadata failure: {Toml}", normToml);
                aggregate.FailedManifests.Add(normToml);
                continue;
            }

            // Cache metadata for reuse (key by normalized manifest path)
            aggregate.ManifestToMetadata[normToml] = metadata;

            var result = this.BuildOwnershipFromMetadata(metadata);

            foreach (var (pkgId, owners) in result.PackageToTomls)
            {
                if (!aggregate.PackageToTomls.TryGetValue(pkgId, out var globalOwners))
                {
                    globalOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    aggregate.PackageToTomls[pkgId] = globalOwners;
                }

                foreach (var ownerToml in owners)
                {
                    globalOwners.Add(ownerToml);
                }
            }

            foreach (var localManifest in result.LocalPackageManifests)
            {
                aggregate.LocalPackageManifests.Add(localManifest);
                visitedManifests.Add(localManifest);
            }

            this.logger.LogInformation(
                "Processed {Toml}: +{LocalCount} local manifests, +{DepCount} deps (aggregate: {AggLocal} manifests, {AggDeps} deps, {MetadataCache} cached)",
                normToml,
                result.LocalPackageManifests.Count,
                result.PackageToTomls.Count,
                aggregate.LocalPackageManifests.Count,
                aggregate.PackageToTomls.Count,
                aggregate.ManifestToMetadata.Count);
        }

        return aggregate;
    }

    private bool IsRustCliManuallyDisabled() =>
        this.envVarService.IsEnvironmentVariableValueTrue("DisableRustCliScan");

    private OwnershipResult BuildOwnershipFromMetadata(CargoMetadata metadata)
    {
        // Step 0: Build dependency graph (package -> deps)
        var graph = metadata.Resolve.Nodes.ToDictionary(
            n => n.Id,
            n => n.Deps.Select(d => d.Pkg).ToList());

        var ownership = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var localManifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Step 1: Gather all local packages (user-owned TOMLs)
        foreach (var pkg in metadata.Packages.Where(p => p.Source == null && !string.IsNullOrEmpty(p.ManifestPath)))
        {
            var manifestPath = this.pathUtilityService.NormalizePath(pkg.ManifestPath);
            localManifests.Add(manifestPath);
            ownership[pkg.Id] = [manifestPath];
        }

        // Step 2: Initialize multi-source BFS queue + inQueue tracker
        var queue = new Queue<string>();
        var inQueue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in ownership.Keys)
        {
            queue.Enqueue(id);
            inQueue.Add(id);
        }

        // Step 3: Propagate ownership
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            inQueue.Remove(currentId); // mark as not queued

            if (!graph.TryGetValue(currentId, out var deps))
            {
                continue;
            }

            var currentOwners = ownership[currentId];

            foreach (var depId in deps)
            {
                if (!ownership.TryGetValue(depId, out var depOwners))
                {
                    depOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    ownership[depId] = depOwners;
                }

                var beforeCount = depOwners.Count;
                depOwners.UnionWith(currentOwners);

                // If ownership expanded and the dep isn't already in queue, enqueue it
                if (depOwners.Count > beforeCount && !inQueue.Contains(depId))
                {
                    queue.Enqueue(depId);
                    inQueue.Add(depId);
                }
            }
        }

        this.logger.LogDebug(
            "Computed ownership for workspace: {LocalCount} local packages, {DepCount} total deps",
            localManifests.Count,
            ownership.Count);

        return new OwnershipResult
        {
            PackageToTomls = ownership,
            LocalPackageManifests = localManifests,
            ManifestToMetadata = new Dictionary<string, CargoMetadata>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private async Task<CargoMetadata> RunCargoMetadataAsync(string manifestPath, CancellationToken token)
    {
        if (!await this.cliService.CanCommandBeLocatedAsync("cargo", null))
        {
            this.logger.LogWarning("Cargo not found while processing {Toml}", manifestPath);
            return null;
        }

        var res = await this.cliService.ExecuteCommandAsync(
            "cargo",
            additionalCandidateCommands: null,
            workingDirectory: null,
            cancellationToken: token,
            "metadata",
            "--manifest-path",
            manifestPath,
            "--format-version=1",
            "--locked");

        if (res.ExitCode != 0)
        {
            this.logger.LogWarning("`cargo metadata` failed for {Toml}: {Err}", manifestPath, res.StdErr);
            return null;
        }

        return CargoMetadata.FromJson(res.StdOut);
    }
}
