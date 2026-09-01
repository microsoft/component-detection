#nullable disable
namespace Microsoft.ComponentDetection.Detectors.CondaLock;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.CondaLock.Contracts;
using MoreLinq;

public static class CondaDependencyResolver
{
    /// <summary>
    /// Registers all packages in he conda-lock file including all dependencies of the package.
    /// This way the full dependency tree will be recoded.
    /// </summary>
    /// <param name="condaLock">The full condaLock object.</param>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    public static void RecordDependencyGraphFromFile(CondaLock condaLock, ISingleFileComponentRecorder singleFileComponentRecorder)
        => GetPackages(condaLock).ForEach(package => RegisterPackageWithDependencies(package, null, condaLock, singleFileComponentRecorder, []));

    /// <summary>
    /// Updates all registered packages that don't have any ancestors.
    /// These packages will be registered as directely referenced.
    /// dependency tree will be recoded.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    public static void UpdateDirectlyReferencedPackages(ISingleFileComponentRecorder singleFileComponentRecorder)
        => singleFileComponentRecorder.GetDetectedComponents().Keys.ForEach(componentId =>
            {
                if (singleFileComponentRecorder.DependencyGraph.GetAncestors(componentId).Count == 0)
                {
                    singleFileComponentRecorder.RegisterUsage(
                        singleFileComponentRecorder.GetComponent(componentId),
                        isExplicitReferencedDependency: true,
                        parentComponentId: null);
                }
            });

    /// <summary>
    /// Register a package a including all of dependencies of the package.
    /// This way the full dependency tree will be recoded.
    /// </summary>
    /// <example>
    /// Assuming the following examplary dependency tree:
    /// A
    ///  \
    ///   C   D
    ///  /|\ /
    /// E F G
    ///
    /// In that case, for package A, this will register:
    ///  1. A -> C -> E
    ///  2. A -> C -> F
    ///  3. A -> C -> G
    /// This happens recursively.
    /// </example>
    /// <param name="package">The package to register.</param>
    /// <param name="parentId">The id of the parent package.</param>
    /// <param name="condaLock">The full condaLock object.</param>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    /// <param name="currentPath">The component ids in the current dependency path.</param>
    private static void RegisterPackageWithDependencies(CondaPackage package, string parentId, CondaLock condaLock, ISingleFileComponentRecorder singleFileComponentRecorder, HashSet<string> currentPath)
    {
        if (package == null)
        {
            return;
        }

        var component = CreateComponent(package);

        //// Register the package itself.
        RegisterPackage(component, parentId, false, singleFileComponentRecorder);

        //// Conda lockfiles can contain dependency cycles; retain the edge above but do not traverse the same path indefinitely.
        if (!currentPath.Add(component.Id))
        {
            return;
        }

        //// Register all dependencies of the package.
        package.Dependencies.Keys.ToList().ForEach(dependency =>
            RegisterPackageWithDependencies(
                condaLock?.Package.FirstOrDefault(condaPackage => condaPackage.Name == dependency && condaPackage.Platform == package.Platform),
                component.Id,
                condaLock,
                singleFileComponentRecorder,
                currentPath));

        currentPath.Remove(component.Id);
    }

    /// <summary>
    /// Registers a package using the SingleFileComponentRecorder.
    /// </summary>
    /// <param name="package">The package to register.</param>
    /// <param name="parentComponentId">The id of the parent of the package.</param>
    /// <param name="isExplicitlyReferenced">Indicating if the package is a direct or transitive dependency.</param>
    /// <param name="singleFileComponentRecorder">The singleFileComponentRecorder.</param>
    private static void RegisterPackage(TypedComponent package, string parentComponentId, bool isExplicitlyReferenced, ISingleFileComponentRecorder singleFileComponentRecorder)
        => singleFileComponentRecorder.RegisterUsage(
                new DetectedComponent(package),
                isExplicitReferencedDependency: isExplicitlyReferenced,
                parentComponentId: parentComponentId);

    /// <summary>
    /// Returns a list of all packages in the given conda-lock file.
    /// </summary>
    /// <param name="condaLock">The full condaLock object that contains a list of all package.</param>
    /// <returns>A list of packages without dependencies.</returns>
    private static List<CondaPackage> GetPackages(CondaLock condaLock)
        => condaLock?.Package == null
                ? []
                : condaLock.Package;

    /// <summary>
    /// Converts a CondaPackage to a TypedComponent.
    /// If the condapackage is a python package it will be converted to a
    /// PipComponent. Otherwise it will be converted to a CondaComponent.
    ///
    /// Conda package metadata is populated from the lock entry and, for older
    /// lock files, from the package URL.
    /// </summary>
    /// <param name="package">The CondaPackage to convert.</param>
    /// <returns>The TypedComponent.</returns>
    private static TypedComponent CreateComponent(CondaPackage package)
    {
        if (IsPythonPackage(package))
        {
            var pipComponent = new PipComponent(package.Name, package.Version);
            if (Uri.TryCreate(package.Url, UriKind.Absolute, out var downloadUrl))
            {
                pipComponent.DownloadUrl = downloadUrl;
            }

            return pipComponent;
        }

        var (channel, subdir, fileName) = GetPackageUrlMetadata(package.Url);
        var md5 = package.Hash != null && package.Hash.TryGetValue("md5", out var hash)
            ? hash
            : null;

        var sha256 = package.Hash != null && package.Hash.TryGetValue("sha256", out var sha256Hash)
            ? sha256Hash
            : null;

        return new CondaComponent(
            name: package.Name,
            version: package.Version,
            build: package.Build ?? GetBuildFromFileName(package, fileName),
            channel: channel,
            subdir: subdir ?? package.Platform,
            @namespace: null,
            url: package.Url,
            md5: md5,
            sha256: sha256);
    }

    private static string GetBuildFromFileName(CondaPackage package, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var packageFileName = fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^8]
            : fileName.EndsWith(".conda", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^6]
                : fileName;
        var buildPrefix = $"{package.Name}-{package.Version}-";

        return packageFileName.StartsWith(buildPrefix, StringComparison.Ordinal)
            ? packageFileName[buildPrefix.Length..]
            : null;
    }

    private static (string Channel, string Subdir, string FileName) GetPackageUrlMetadata(string packageUrl)
    {
        if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
        {
            return (null, null, null);
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return (null, null, null);
        }

        var channelUri = new UriBuilder(uri)
        {
            Path = segments.Length == 2 ? "/" : $"/{string.Join("/", segments[..^2])}",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

        return (
            channelUri,
            Uri.UnescapeDataString(segments[^2]),
            Uri.UnescapeDataString(segments[^1]));
    }

    /// <summary>
    /// Checks if a package is a python package.
    ///
    /// If the package is either managed by pip, or if it depends on python
    /// it is considered a python package.
    /// </summary>
    /// <param name="package">The CondaPackage.</param>
    /// <returns>True if the package is a python package.</returns>
    private static bool IsPythonPackage(CondaPackage package)
        => package.Manager?.Equals("pip", StringComparison.OrdinalIgnoreCase) == true ||
           package.Dependencies?.Keys.Any(dependency => dependency.Equals("python", StringComparison.OrdinalIgnoreCase)) == true;
}
