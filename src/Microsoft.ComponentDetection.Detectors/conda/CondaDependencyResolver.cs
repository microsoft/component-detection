namespace Microsoft.ComponentDetection.Detectors.Poetry;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

public static class CondaDependencyResolver
{
    /// <summary>
    /// Iterate through all dependencies that are explicitly referenced in the conda environment
    /// and build as well as register the full dependency tree for each of them.
    /// </summary>
    /// <param name="condaLock">The full condaLock object.</param>
    /// <param name="explicitDependencies">The list of all dependencies that are explicitly specified in the conda environment.</param>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    public static void RecordDependencyGraphFromFile(CondaLock condaLock, List<string> explicitDependencies, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        if (condaLock != null && condaLock.Package != null)
        {
            condaLock.Package.ForEach(package =>
            {
                var isExplicitDependency = explicitDependencies.Any(dependency => dependency.Contains(package.Name)) || !explicitDependencies.Any();

                // Register package as direct as explicit dependency if it is either
                // referenced in the parsed environment.yml, or if the environment.yml
                // was not parsed successful and thus explicitDependencies is empty.
                // In case explicitDependencies is empty. We will register every
                // package as explicit dependency, to ensure alerts will still be generated.
                if (isExplicitDependency)
                {
                    RecordPackage(package, true, package.Platform, null, condaLock, singleFileComponentRecorder);
                }
            });
        }
    }

    /// <summary>
    /// Recursively walk through the dependency tree.
    /// </summary>
    /// <param name="dependencyName">The name of the dependency that should be expanded.</param>
    /// <param name="dependencyPlatform">The platform of the dependency that should expanded.</param>
    /// <param name="parentId">The id of the parent component of the dependency that should expanded.</param>
    /// <param name="condaLock">The full condaLock object.</param>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    public static void RecordTransitiveDependencies(string dependencyName, string dependencyPlatform, string parentId, CondaLock condaLock, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        condaLock.Package.ForEach(package =>
        {
            if (package.Name == dependencyName && package.Platform == dependencyPlatform)
            {
                RecordPackage(package, false, dependencyPlatform, parentId, condaLock, singleFileComponentRecorder);
            }
        });
    }

    /// <summary>
    /// Registers the usage of a package.
    /// </summary>
    /// <param name="package">The conda package to register.</param>
    /// <param name="isExplicitDependency">True if the package is explicitly referenced.</param>
    /// <param name="dependencyPlatform">The platform of the dependency that should expanded.</param>
    /// <param name="parentId">The id of the parent component of the dependency that should expanded.</param>
    /// <param name="condaLock">The full condaLock object.</param>
    /// <param name="singleFileComponentRecorder">The SingleFileComponentRecorder.</param>
    public static void RecordPackage(CondaPackage package, bool isExplicitDependency, string dependencyPlatform, string parentId, CondaLock condaLock, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var condaLockComponent = CreateCondaLockComponentFromPackage(package);
        var pipComponent = CreatePipComponentFromPackage(package);
        var componentId = package.Manager == "pip" ? pipComponent.Id : condaLockComponent.Id;

        singleFileComponentRecorder.RegisterUsage(
            new DetectedComponent(package.Manager == "pip" ? pipComponent : condaLockComponent),
            isExplicitReferencedDependency: isExplicitDependency,
            parentComponentId: parentId);

        package.Dependencies.Keys.ToList().ForEach(dependency =>
        {
            RecordTransitiveDependencies(dependency, dependencyPlatform, componentId, condaLock, singleFileComponentRecorder);
        });
    }

    /// <summary>
    /// Converts a CondaPackage to a CondaLockComponent.
    /// </summary>
    /// <param name="package">The CondaPackage to convert.</param>
    /// <returns>The CondaLockComponent.</returns>
    public static CondaLockComponent CreateCondaLockComponentFromPackage(CondaPackage package)
        => new CondaLockComponent(
            package.Name,
            package.Version,
            package.Category,
            package.Dependencies,
            package.Hash,
            package.Manager,
            package.Optional,
            package.Platform,
            package.Url);

    /// <summary>
    /// Converts a CondaPackage to a PipComponent.
    /// </summary>
    /// <param name="package">The CondaPackage to convert.</param>
    /// <returns>The CondaLockComponent.</returns>
    public static PipComponent CreatePipComponentFromPackage(CondaPackage package)
        => new PipComponent(
            package.Name,
            package.Version);
}
