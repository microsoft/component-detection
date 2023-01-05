using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

namespace Microsoft.ComponentDetection.Detectors.Maven;

public interface IMavenCommandService
{
    string BcdeMvnDependencyFileName { get; }

    Task<bool> MavenCLIExists();

    Task GenerateDependenciesFile(ProcessRequest processRequest);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
