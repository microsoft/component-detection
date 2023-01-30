namespace Microsoft.ComponentDetection.Detectors.Maven;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

public interface IMavenCommandService
{
    string BcdeMvnDependencyFileName { get; }

    Task<bool> MavenCLIExistsAsync();

    Task GenerateDependenciesFileAsync(ProcessRequest processRequest);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
