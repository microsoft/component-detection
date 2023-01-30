namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public interface IPythonCommandService
{
    Task<bool> PythonExists(string pythonPath = null);

    Task<IList<(string PackageString, GitComponent Component)>> ParseFile(string path, string pythonPath = null);
}
