#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Represents a mapping between a Docker layer and the components detected within that layer.
/// This class associates components (both Linux system packages and application-level packages) with their corresponding Docker layer.
/// </summary>
public class LayerMappedLinuxComponents
{
    /// <summary>
    /// Gets or sets the components detected in this layer.
    /// This can include system packages (LinuxComponent) as well as application-level packages (NpmComponent, PipComponent, etc.).
    /// </summary>
    public IEnumerable<TypedComponent> Components { get; set; }

    /// <summary>
    /// Gets or sets the Docker layer associated with these components.
    /// </summary>
    public DockerLayer DockerLayer { get; set; }
}
