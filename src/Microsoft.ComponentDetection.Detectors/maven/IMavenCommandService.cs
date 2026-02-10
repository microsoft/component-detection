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

    void ParseDependenciesFile(ProcessRequest processRequest);

    /// <summary>
    /// Clears the internal caches (location locks and completed locations).
    /// Should be called at the end of each scan to prevent unbounded memory growth.
    /// </summary>
    void ClearCache();
}
