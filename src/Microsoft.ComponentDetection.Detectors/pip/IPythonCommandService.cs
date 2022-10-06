using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponentNS;

namespace Microsoft.ComponentDetection.Detectors.Pip
{
    public interface IPythonCommandService
    {
        Task<bool> PythonExists(string pythonPath = null);

        Task<IList<(string, GitComponent)>> ParseFile(string path, string pythonPath = null);
    }
}
