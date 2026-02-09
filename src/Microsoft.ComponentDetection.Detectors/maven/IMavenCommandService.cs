#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

public interface IMavenCommandService
{
    string BcdeMvnDependencyFileName { get; }

    Task<bool> MavenCLIExistsAsync();

    Task<MavenCliResult> GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default);

    Task<MavenCliResult> GenerateDependenciesFileAsync(ProcessRequest processRequest, string outputFileName, CancellationToken cancellationToken = default);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
