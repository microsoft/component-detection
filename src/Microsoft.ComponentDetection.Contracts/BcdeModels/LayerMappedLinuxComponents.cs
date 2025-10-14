#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class LayerMappedLinuxComponents
{
    public IEnumerable<LinuxComponent> LinuxComponents { get; set; }

    public DockerLayer DockerLayer { get; set; }
}
