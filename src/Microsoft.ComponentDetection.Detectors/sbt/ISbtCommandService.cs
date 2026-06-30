#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Sbt;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

public interface ISbtCommandService
{
    string BcdeSbtDependencyFileName { get; }

    Task<bool> SbtCLIExistsAsync();

    Task GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default);

    void ParseDependenciesFile(ProcessRequest processRequest);
}
