#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

public interface IRustSbomParser
{
    public Task<int?> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken);

    public Task<int?> ParseWithOwnershipAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder sbomRecorder,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        CancellationToken cancellationToken);
}
