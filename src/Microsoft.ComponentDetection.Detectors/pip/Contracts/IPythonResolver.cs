#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

public interface IPythonResolver
{
    /// <summary>
    /// Resolves the root Python packages from the initial list of packages.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    /// <param name="initialPackages">The initial list of packages.</param>
    /// <returns>The root packages, with dependencies associated as children.</returns>
    Task<IList<PipGraphNode>> ResolveRootsAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IList<PipDependencySpecification> initialPackages);

    /// <summary>
    /// Sets a python environment variable used for conditional dependency checks.
    /// </summary>
    /// <param name="key">The key for a variable to be stored.</param>
    /// <param name="value">the value to be stored for that key.</param>
    void SetPythonEnvironmentVariable(string key, string value);

    /// <summary>
    /// Retrieves a the dictionary of python environment variables used for conditional dependency checks.
    /// </summary>
    /// <returns> the dictionary of stored python environment variables else null if not stored.</returns>
    Dictionary<string, string> GetPythonEnvironmentVariables();
}
