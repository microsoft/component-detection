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
    /// Registers that a detector is actively reading a dependency file.
    /// This prevents premature deletion by other detectors.
    /// </summary>
    /// <param name="dependencyFilePath">The path to the dependency file being read.</param>
    void RegisterFileReader(string dependencyFilePath);

    /// <summary>
    /// Unregisters a detector's active reading of a dependency file and attempts cleanup.
    /// If no other detectors are reading the file, it will be safely deleted.
    /// </summary>
    /// <param name="dependencyFilePath">The path to the dependency file that was being read.</param>
    /// <param name="detectorId">The identifier of the detector unregistering the file reader.</param>
    void UnregisterFileReader(string dependencyFilePath, string detectorId = null);
}
