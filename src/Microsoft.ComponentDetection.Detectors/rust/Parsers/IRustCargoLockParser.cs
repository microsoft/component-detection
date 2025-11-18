#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

public interface IRustCargoLockParser
{
    public Task<int?> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        CancellationToken cancellationToken);
}
