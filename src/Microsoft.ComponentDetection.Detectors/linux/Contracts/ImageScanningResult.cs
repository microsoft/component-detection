using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts;

internal class ImageScanningResult
{
    public ContainerDetails ContainerDetails { get; set; }

    public IEnumerable<DetectedComponent> Components { get; set; }
}
