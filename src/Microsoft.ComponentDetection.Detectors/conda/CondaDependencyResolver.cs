namespace Microsoft.ComponentDetection.Detectors.Poetry;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

public static class CondaDependencyResolver
{
    /// <summary>
    /// Iterate through all dependencis that are explicitly referenced in the conda environment
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
                var isExplicitDependency = explicitDependencies.Any(dependency => dependency.Contains(package.Name));

                if (isExplicitDependency || !explicitDependencies.Any())
                {
                    var condaComponent = CreateCondaComponentFromPackage(package);

                    singleFileComponentRecorder.RegisterUsage(
                        new DetectedComponent(condaComponent),
                        isExplicitReferencedDependency: isExplicitDependency);

                    package.Dependencies.Keys.ToList().ForEach(dependency =>
                    {
                        RecordTransitiveDependencies(dependency, package.Platform, condaComponent.Id, condaLock, singleFileComponentRecorder);
                    });
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
                var condaComponent = CreateCondaComponentFromPackage(package);

                singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(condaComponent),
                    isExplicitReferencedDependency: false,
                    parentComponentId: parentId);

                package.Dependencies.Keys.ToList().ForEach(dependency =>
                {
                    RecordTransitiveDependencies(dependency, dependencyPlatform, condaComponent.Id, condaLock, singleFileComponentRecorder);
                });
            }
        });
    }

    /// <summary>
    /// Converts a CondaPackage to a CondaCompinent.
    /// </summary>
    /// <param name="package">The CondaPackage to convert.</param>
    /// <returns>The CondaComponent.</returns>
    public static CondaComponent CreateCondaComponentFromPackage(CondaPackage package)
        => new CondaComponent(
            package.Name,
            package.Version,
            package.Category,
            package.Dependencies,
            package.Hash,
            package.Manager,
            package.Optional,
            package.Platform,
            package.Url);
}
