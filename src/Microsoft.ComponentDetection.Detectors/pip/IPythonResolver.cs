namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IPythonResolver
{
    /// <summary>
    /// Resolves the root Python packages from the initial list of packages.
    /// </summary>
    /// <param name="initialPackages">The initial list of packages.</param>
    /// <returns>The root packages, with dependencies associated as children.</returns>
    Task<IList<PipGraphNode>> ResolveRoots(IList<PipDependencySpecification> initialPackages);
}
