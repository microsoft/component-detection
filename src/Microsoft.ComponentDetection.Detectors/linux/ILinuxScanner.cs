namespace Microsoft.ComponentDetection.Detectors.Linux;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Interface for scanning Linux container layers to identify components.
/// </summary>
public interface ILinuxScanner
{
    /// <summary>
    /// Scans a Linux container image for components and maps them to their respective layers.
    /// Runs Syft and processes the output in a single step.
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

    /// <summary>
    /// Runs the Syft scanner and returns the raw parsed output without processing components.
    /// Use this when the caller needs access to the full Syft output (e.g., to extract source metadata for OCI images).
    /// </summary>
    /// <param name="syftSource">The source argument passed to Syft (e.g., an image hash or "oci-dir:/oci-image").</param>
    /// <param name="additionalBinds">Additional volume bind mounts for the Syft container (e.g., for mounting OCI directories).</param>
    /// <param name="scope">The scope for scanning the image.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the parsed <see cref="SyftOutput"/>.</returns>
    public Task<SyftOutput> GetSyftOutputAsync(
        string syftSource,
        IList<string> additionalBinds,
        LinuxScannerScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Processes parsed Syft output into layer-mapped components.
    /// </summary>
    /// <param name="syftOutput">The parsed Syft output.</param>
    /// <param name="containerLayers">The layers to map components to.</param>
    /// <param name="enabledComponentTypes">The set of component types to include in the results.</param>
    /// <returns>A collection of <see cref="LayerMappedLinuxComponents"/> representing the components found and their associated layers.</returns>
    public IEnumerable<LayerMappedLinuxComponents> ProcessSyftOutput(
        SyftOutput syftOutput,
        IEnumerable<DockerLayer> containerLayers,
        ISet<ComponentType> enabledComponentTypes
    );
}
