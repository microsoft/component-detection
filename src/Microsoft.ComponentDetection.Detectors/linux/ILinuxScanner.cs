using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Detectors.Linux;

public interface ILinuxScanner
{
    Task<IEnumerable<LayerMappedLinuxComponents>> ScanLinuxAsync(string imageHash, IEnumerable<DockerLayer> dockerLayers, int baseImageLayerCount, CancellationToken cancellationToken = default);
}
