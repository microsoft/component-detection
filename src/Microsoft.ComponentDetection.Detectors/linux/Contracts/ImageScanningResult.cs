#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
/// Represents the result of scanning a container image for components.
/// </summary>
internal class ImageScanningResult
{
    /// <summary>
    /// Gets or sets the container details associated with the image scanning result.
    /// </summary>
    public ContainerDetails ContainerDetails { get; set; }

    /// <summary>
    /// Gets or sets the collection of components detected during the image scanning process.
    /// </summary>
    public IEnumerable<DetectedComponent> Components { get; set; }
}
