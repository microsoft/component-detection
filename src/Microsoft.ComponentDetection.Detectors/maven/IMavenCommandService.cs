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

    /// <summary>
    /// Generates a Maven dependency file using a custom local repository path.
    /// This allows running multiple Maven processes in parallel without file locking conflicts.
    /// </summary>
    /// <param name="processRequest">The process request containing the pom.xml file.</param>
    /// <param name="outputFileName">The output file name for the dependency tree.</param>
    /// <param name="localRepositoryPath">Custom path for Maven's local repository (instead of ~/.m2/repository).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and any error output.</returns>
    Task<MavenCliResult> GenerateDependenciesFileAsync(ProcessRequest processRequest, string outputFileName, string localRepositoryPath, CancellationToken cancellationToken = default);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
