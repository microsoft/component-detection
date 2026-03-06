namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::NuGet.Frameworks;
using global::NuGet.Packaging.Core;
using global::NuGet.ProjectModel;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared utility methods for processing NuGet lock files (project.assets.json).
/// Used by both NuGetProjectModelProjectCentricComponentDetector and MSBuildBinaryLogComponentDetector.
/// </summary>
public static class LockFileUtilities
{
    /// <summary>
    /// Dependency type constant for project references in project.assets.json.
    /// </summary>
    public const string ProjectDependencyType = "project";

    /// <summary>
    /// Gets the framework references for a given lock file target.
    /// </summary>
    /// <param name="lockFile">The lock file to analyze.</param>
    /// <param name="target">The target framework to get references for.</param>
    /// <returns>Array of framework reference names.</returns>
    public static string[] GetFrameworkReferences(LockFile lockFile, LockFileTarget target)
    {
        var frameworkInformation = lockFile.PackageSpec?.TargetFrameworks
            .FirstOrDefault(x => x.FrameworkName.Equals(target.TargetFramework));

        if (frameworkInformation == null)
        {
            return [];
        }

        // Add directly referenced frameworks
        var results = frameworkInformation.FrameworkReferences.Select(x => x.Name);

        // Add transitive framework references
        results = results.Concat(target.Libraries.SelectMany(l => l.FrameworkReferences));

        return results.Distinct().ToArray();
    }

    /// <summary>
    /// Determines if a library is a development dependency based on its content.
    /// A placeholder item is an empty file that doesn't exist with name _._ meant to indicate
    /// an empty folder in a nuget package, but also used by NuGet when a package's assets are excluded.
    /// </summary>
    /// <param name="library">The library to check.</param>
    /// <param name="lockFile">The lock file containing library metadata.</param>
    /// <returns>True if the library is a development dependency.</returns>
    public static bool IsADevelopmentDependency(LockFileTargetLibrary library, LockFile lockFile)
    {
        static bool IsAPlaceholderItem(LockFileItem item) =>
            Path.GetFileName(item.Path).Equals(PackagingCoreConstants.EmptyFolder, StringComparison.OrdinalIgnoreCase);

        // All(IsAPlaceholderItem) checks if the collection is empty or all items are placeholders.
        return library.RuntimeAssemblies.All(IsAPlaceholderItem) &&
            library.RuntimeTargets.All(IsAPlaceholderItem) &&
            library.ResourceAssemblies.All(IsAPlaceholderItem) &&
            library.NativeLibraries.All(IsAPlaceholderItem) &&
            library.ContentFiles.All(IsAPlaceholderItem) &&
            library.Build.All(IsAPlaceholderItem) &&
            library.BuildMultiTargeting.All(IsAPlaceholderItem) &&

            // The SDK looks at the library for analyzers using the following heuristic:
            // https://github.com/dotnet/sdk/blob/d7fe6e66d8f67dc93c5c294a75f42a2924889196/src/Tasks/Microsoft.NET.Build.Tasks/NuGetUtils.NuGet.cs#L43
            (!lockFile.GetLibrary(library.Name, library.Version)?.Files
                .Any(file => file.StartsWith("analyzers", StringComparison.Ordinal)
                    && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)) ?? false);
    }

    /// <summary>
    /// Gets the top-level libraries (direct dependencies) from a lock file.
    /// </summary>
    /// <param name="lockFile">The lock file to analyze.</param>
    /// <returns>List of top-level library information.</returns>
    public static List<(string Name, Version? Version, VersionRange? VersionRange)> GetTopLevelLibraries(LockFile lockFile)
    {
        var toBeFilled = new List<(string Name, Version? Version, VersionRange? VersionRange)>();

        if (lockFile.PackageSpec?.TargetFrameworks != null)
        {
            foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
            {
                foreach (var dependency in framework.Dependencies)
                {
                    toBeFilled.Add((dependency.Name, Version: null, dependency.LibraryRange.VersionRange));
                }
            }
        }

        var projectDirectory = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath != null
            ? Path.GetDirectoryName(lockFile.PackageSpec.RestoreMetadata.ProjectPath)
            : null;

        if (projectDirectory != null && lockFile.Libraries != null)
        {
            var librariesWithAbsolutePath = lockFile.Libraries
                .Where(x => x.Type == ProjectDependencyType)
                .Select(x => (library: x, absoluteProjectPath: Path.GetFullPath(Path.Combine(projectDirectory, x.Path))))
                .ToDictionary(x => x.absoluteProjectPath, x => x.library);

            if (lockFile.PackageSpec?.RestoreMetadata?.TargetFrameworks != null)
            {
                foreach (var restoreMetadataTargetFramework in lockFile.PackageSpec.RestoreMetadata.TargetFrameworks)
                {
                    foreach (var projectReference in restoreMetadataTargetFramework.ProjectReferences)
                    {
                        if (librariesWithAbsolutePath.TryGetValue(Path.GetFullPath(projectReference.ProjectPath), out var library))
                        {
                            toBeFilled.Add((library.Name, library.Version?.Version, null));
                        }
                    }
                }
            }
        }

        return toBeFilled;
    }

    /// <summary>
    /// Looks up a library in project.assets.json given a version (preferred) or version range.
    /// </summary>
    /// <param name="libraries">The list of libraries to search.</param>
    /// <param name="dependencyId">The dependency name to find.</param>
    /// <param name="version">The specific version to match (mutually exclusive with versionRange).</param>
    /// <param name="versionRange">The version range to match (mutually exclusive with version).</param>
    /// <param name="logger">Optional logger for debug messages.</param>
    /// <returns>The matching library, or null if not found.</returns>
    public static LockFileLibrary? GetLibraryComponentWithDependencyLookup(
        IList<LockFileLibrary>? libraries,
        string dependencyId,
        Version? version,
        VersionRange? versionRange,
        ILogger? logger = null)
    {
        if (libraries == null)
        {
            return null;
        }

        if ((version == null && versionRange == null) || (version != null && versionRange != null))
        {
            logger?.LogDebug("Either version or versionRange must be specified, but not both for {DependencyId}.", dependencyId);
            return null;
        }

        var matchingLibraryNames = libraries.Where(x => string.Equals(x.Name, dependencyId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matchingLibraryNames.Count == 0)
        {
            logger?.LogDebug("No library found matching: {DependencyId}", dependencyId);
            return null;
        }

        LockFileLibrary? matchingLibrary;
        if (version != null)
        {
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => x.Version?.Version?.Equals(version) ?? false);
        }
        else
        {
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => x.Version != null && versionRange!.Satisfies(x.Version));
        }

        if (matchingLibrary == null)
        {
            matchingLibrary = matchingLibraryNames.First();
            var versionString = versionRange != null ? versionRange.ToNormalizedString() : version?.ToString();
            logger?.LogDebug(
                "Couldn't satisfy lookup for {Version}. Falling back to first found component for {MatchingLibraryName}, resolving to version {MatchingLibraryVersion}.",
                versionString,
                matchingLibrary.Name,
                matchingLibrary.Version);
        }

        return matchingLibrary;
    }

    /// <summary>
    /// Navigates the dependency graph and registers components with the component recorder.
    /// </summary>
    /// <param name="target">The lock file target containing dependency information.</param>
    /// <param name="explicitlyReferencedComponentIds">Set of component IDs that are explicitly referenced.</param>
    /// <param name="singleFileComponentRecorder">The component recorder to register with.</param>
    /// <param name="library">The library to process.</param>
    /// <param name="parentComponentId">The parent component ID, or null for root dependencies.</param>
    /// <param name="isDevelopmentDependency">Function to determine if a library is a development dependency.</param>
    /// <param name="visited">Set of already visited dependency IDs to prevent cycles.</param>
    public static void NavigateAndRegister(
        LockFileTarget target,
        HashSet<string> explicitlyReferencedComponentIds,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        LockFileTargetLibrary library,
        string? parentComponentId,
        Func<LockFileTargetLibrary, bool> isDevelopmentDependency,
        HashSet<string>? visited = null)
    {
        if (library.Type == ProjectDependencyType)
        {
            return;
        }

        visited ??= [];

        var libraryComponent = new DetectedComponent(new NuGetComponent(library.Name, library.Version?.ToNormalizedString() ?? "0.0.0"));

        singleFileComponentRecorder.RegisterUsage(
            libraryComponent,
            explicitlyReferencedComponentIds.Contains(libraryComponent.Component.Id),
            parentComponentId,
            isDevelopmentDependency: isDevelopmentDependency(library),
            targetFramework: target.TargetFramework?.GetShortFolderName());

        foreach (var dependency in library.Dependencies)
        {
            if (visited.Contains(dependency.Id))
            {
                continue;
            }

            var targetLibrary = target.GetTargetLibrary(dependency.Id);

            if (targetLibrary != null)
            {
                visited.Add(dependency.Id);
                NavigateAndRegister(
                    target,
                    explicitlyReferencedComponentIds,
                    singleFileComponentRecorder,
                    targetLibrary,
                    libraryComponent.Component.Id,
                    isDevelopmentDependency,
                    visited);
            }
        }
    }

    /// <summary>
    /// Registers PackageDownload dependencies from the lock file.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder to register with.</param>
    /// <param name="lockFile">The lock file containing PackageDownload references.</param>
    /// <param name="isDevelopmentDependency">
    /// Optional callback to determine if a package download is a development dependency.
    /// Parameters are (packageName, targetFramework). Defaults to always returning true since
    /// PackageDownload usage does not make it part of the application.
    /// </param>
    public static void RegisterPackageDownloads(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        LockFile lockFile,
        Func<string, NuGetFramework?, bool>? isDevelopmentDependency = null)
    {
        if (lockFile.PackageSpec?.TargetFrameworks == null)
        {
            return;
        }

        // Default: PackageDownload is always a development dependency
        isDevelopmentDependency ??= (_, _) => true;

        foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
        {
            var tfm = framework.FrameworkName;

            foreach (var packageDownload in framework.DownloadDependencies)
            {
                if (packageDownload?.Name is null || packageDownload?.VersionRange?.MinVersion is null)
                {
                    continue;
                }

                var libraryComponent = new DetectedComponent(new NuGetComponent(packageDownload.Name, packageDownload.VersionRange.MinVersion.ToNormalizedString()));

                singleFileComponentRecorder.RegisterUsage(
                    libraryComponent,
                    isExplicitReferencedDependency: true,
                    parentComponentId: null,
                    isDevelopmentDependency: isDevelopmentDependency(packageDownload.Name, tfm),
                    targetFramework: tfm?.GetShortFolderName());
            }
        }
    }
}
