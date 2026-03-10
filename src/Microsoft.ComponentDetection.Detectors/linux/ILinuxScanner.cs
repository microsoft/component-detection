#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Interface for scanning Linux container layers to identify components.
/// </summary>
public interface ILinuxScanner
{
    /// <summary>
    /// Scans a Linux container image for components and maps them to their respective layers.
    /// </summary>
    /// <param name="imageHash">The hash identifier of the container image to scan.</param>
    /// <param name="containerLayers">The collection of Docker layers that make up the container image.</param>
    /// <param name="baseImageLayerCount">The number of layers that belong to the base image, used to distinguish base image layers from application layers.</param>
    /// <param name="enabledComponentTypes">The set of component types to include in the scan results. Only components matching these types will be returned.</param>
    /// <param name="scope">The scope for scanning the image. See <see cref="LinuxScannerScope"/> for values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see cref="LayerMappedLinuxComponents"/> representing the components found in the image and their associated layers.</returns>
    public Task<IEnumerable<LayerMappedLinuxComponents>> ScanLinuxAsync(
        string imageHash,
        IEnumerable<DockerLayer> containerLayers,
        int baseImageLayerCount,
        ISet<ComponentType> enabledComponentTypes,
        LinuxScannerScope scope,
        CancellationToken cancellationToken = default
    );
}
