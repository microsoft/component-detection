#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

public interface IMavenCommandService
{
    string BcdeMvnDependencyFileName { get; }

    Task<bool> MavenCLIExistsAsync();

    Task GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
