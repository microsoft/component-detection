namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Abstract base class for system package detectors (RPM, APK, DPKG, etc.).
/// </summary>
public abstract class SystemPackageDetector : FileComponentDetector
{
    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default
    )
    {
        // Only run on Linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            this.Logger.LogDebug("Skipping {DetectorId} - not running on Linux", this.Id);
            return;
        }

        var file = processRequest.ComponentStream;
        var recorder = processRequest.SingleFileComponentRecorder;

        try
        {
            // Find the Linux distribution
            var distro = await this.FindDistributionAsync().ConfigureAwait(false);

            if (distro == null)
            {
                this.Logger.LogWarning(
                    "Could not determine Linux distribution for {FilePath}, using 'linux' as default namespace",
                    file.Location
                );
            }

            // Parse packages from the database
            var packages = await this.ParsePackagesAsync(file.Stream, file.Location, distro)
                .ConfigureAwait(false);

            if (packages.Count == 0)
            {
                this.Logger.LogDebug("No packages found in {FilePath}", file.Location);
                return;
            }

            // Build dependency graph and register components
            this.BuildDependencyGraph(packages, recorder, distro);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(
                ex,
                "Error processing system package database at {FilePath}",
                file.Location
            );
            throw;
        }
    }

    /// <summary>
    /// Parses packages from the system package database.
    /// </summary>
    /// <param name="dbStream">The database file stream.</param>
    /// <param name="location">The location of the database file.</param>
    /// <param name="distro">The detected Linux distribution.</param>
    /// <returns>A list of parsed package information.</returns>
    protected abstract Task<List<SystemPackageInfo>> ParsePackagesAsync(
        Stream dbStream,
        string location,
        LinuxDistribution distro
    );

    /// <summary>
    /// Creates a TypedComponent from system package information.
    /// </summary>
    /// <param name="package">The package information.</param>
    /// <param name="distro">The Linux distribution.</param>
    /// <returns>A TypedComponent representing the package.</returns>
    protected abstract TypedComponent CreateComponent(
        SystemPackageInfo package,
        LinuxDistribution distro
    );

    /// <summary>
    /// Finds the Linux distribution by looking for os-release files relative to the database location.
    /// </summary>
    /// <returns>A LinuxDistribution object or null if not found.</returns>
    protected virtual async Task<LinuxDistribution> FindDistributionAsync()
    {
        // Try common os-release locations relative to the database
        var possiblePaths = new[] { "/etc/os-release", "/usr/lib/os-release" };

        foreach (var path in possiblePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    var distro = LinuxDistribution.ParseOsRelease(content);
                    if (distro is not null)
                    {
                        this.Logger.LogDebug(
                            "Found Linux distribution: {Id} {VersionId} at {Path}",
                            distro.Id,
                            distro.VersionId,
                            path
                        );
                        return distro;
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogTrace(ex, "Failed to read os-release file at {Path}", path);
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the dependency graph from package information using Provides/Requires relationships.
    /// </summary>
    /// <param name="packages">The list of packages to process.</param>
    /// <param name="recorder">The component recorder.</param>
    /// <param name="distro">The Linux distribution.</param>
    protected virtual void BuildDependencyGraph(
        List<SystemPackageInfo> packages,
        ISingleFileComponentRecorder recorder,
        LinuxDistribution distro
    )
    {
        // Create a provides index: capability -> list of packages that provide it
        var providesIndex = new Dictionary<string, List<SystemPackageInfo>>(packages.Count);

        // Index all packages by what they provide
        foreach (var pkg in packages)
        {
            // Package name is always a "provides"
            if (!providesIndex.TryGetValue(pkg.Name, out var pkgList))
            {
                pkgList = [];
                providesIndex[pkg.Name] = pkgList;
            }

            pkgList.Add(pkg);

            // Add explicit provides
            if (pkg.Provides is not null)
            {
                foreach (var provides in pkg.Provides)
                {
                    if (string.IsNullOrWhiteSpace(provides))
                    {
                        continue;
                    }

                    if (!providesIndex.TryGetValue(provides, out var providesList))
                    {
                        providesList = [];
                        providesIndex[provides] = providesList;
                    }

                    providesList.Add(pkg);
                }
            }
        }

        // Create components and track them by package name
        var componentsByPackageName = new Dictionary<string, DetectedComponent>(packages.Count);

        // First pass: register all components as root dependencies
        foreach (var pkg in packages)
        {
            var component = new DetectedComponent(this.CreateComponent(pkg, distro));
            recorder.RegisterUsage(component, isExplicitReferencedDependency: true);
            componentsByPackageName[pkg.Name] = component;
        }

        // Second pass: add dependency relationships
        foreach (var pkg in packages)
        {
            if (!componentsByPackageName.TryGetValue(pkg.Name, out var childComponent))
            {
                continue;
            }

            if (pkg.Requires is not null)
            {
                foreach (var require in pkg.Requires)
                {
                    if (string.IsNullOrWhiteSpace(require))
                    {
                        continue;
                    }

                    // Skip boolean expressions (not supported)
                    if (require.TrimStart().StartsWith('('))
                    {
                        continue;
                    }

                    // Find packages that provide this requirement
                    if (providesIndex.TryGetValue(require, out var providers))
                    {
                        foreach (var provider in providers)
                        {
                            // Skip self-references
                            if (provider.Name == pkg.Name)
                            {
                                continue;
                            }

                            if (
                                componentsByPackageName.TryGetValue(
                                    provider.Name,
                                    out var parentComponent
                                )
                            )
                            {
                                // Register the dependency relationship
                                recorder.RegisterUsage(
                                    childComponent,
                                    isExplicitReferencedDependency: false,
                                    parentComponentId: parentComponent.Component.Id
                                );
                            }
                        }
                    }
                }
            }
        }

        this.Logger.LogInformation(
            "Registered {PackageCount} packages with dependency relationships",
            packages.Count
        );
    }

    /// <summary>
    /// Represents package information extracted from a system package database.
    /// </summary>
    protected class SystemPackageInfo
    {
        public required string Name { get; init; }

        public required string Version { get; init; }

        public List<string> Provides { get; init; } = [];

        public List<string> Requires { get; init; } = [];

        public object Metadata { get; init; }
    }
}
