#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

public interface ISimplePythonResolver
{
    /// <summary>
    /// Resolves the root Python packages from the initial list of packages.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    /// <param name="initialPackages">The initial list of packages.</param>
    /// <returns>The root packages, with dependencies associated as children.</returns>
    Task<IList<PipGraphNode>> ResolveRootsAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IList<PipDependencySpecification> initialPackages);
}
