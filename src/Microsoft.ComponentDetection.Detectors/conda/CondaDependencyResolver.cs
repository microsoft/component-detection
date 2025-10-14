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
        => GetPackages(condaLock).ForEach(package => RegisterPackageWithDependencies(package, null, condaLock, singleFileComponentRecorder));

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
    private static void RegisterPackageWithDependencies(CondaPackage package, string parentId, CondaLock condaLock, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        if (package == null)
        {
            return;
        }

        var component = CreateComponent(package);

        //// Register the package itself.
        RegisterPackage(component, parentId, false, singleFileComponentRecorder);

        //// Register all dependencies of the package.
        package.Dependencies.Keys.ToList().ForEach(dependency =>
            RegisterPackageWithDependencies(
                condaLock?.Package.FirstOrDefault(condaPackage => condaPackage.Name == dependency && condaPackage.Platform == package.Platform),
                component.Id,
                condaLock,
                singleFileComponentRecorder));
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
    /// For the conda component, only the most necessary information that are
    /// required for component governance are passed. This way duplicates
    /// will be avoided. For example the URL of the package is platform-specific.
    /// That is, the same package with the same version will exist multiple
    /// times in the conda-lock files with different urls. If we were to add
    /// the url, we would register these duplicates. As the information is
    /// anyway not required for compoment governance, we don't pass it and
    /// this way avoid duplicates.
    ///
    /// </summary>
    /// <param name="package">The CondaPackage to convert.</param>
    /// <returns>The TypedComponent.</returns>
    private static TypedComponent CreateComponent(CondaPackage package)
        => IsPythonPackage(package)
                ? new PipComponent(package.Name, package.Version)
                : new CondaComponent(package.Name, package.Version, null, package.Category, null, null, null, null);

    /// <summary>
    /// Checks if a package is a python package.
    ///
    /// If the package is either managed by pip, or if it depends on python
    /// it is considered a python package.
    /// </summary>
    /// <param name="package">The CondaPackage.</param>
    /// <returns>True if the package is a python package.</returns>
    private static bool IsPythonPackage(CondaPackage package)
        => package.Manager.Equals("pip", StringComparison.OrdinalIgnoreCase) ||
           package.Dependencies.Keys.Any(dependency => dependency.Equals("python", StringComparison.OrdinalIgnoreCase));
}
