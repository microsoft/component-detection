#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public interface IPythonCommandService
{
    Task<bool> PythonExistsAsync(string pythonPath = null);

    Task<IList<(string PackageString, GitComponent Component)>> ParseFileAsync(string path, string pythonPath = null);

    Task<string> GetPythonVersionAsync(string pythonPath = null);

    /// <summary>
    /// Gets the os type using: https://docs.python.org/3/library/sys.html#sys.platform .
    /// </summary>
    /// <returns>OS type where the python script runs.</returns>
    Task<string> GetOsTypeAsync(string pythonPath = null);
}
