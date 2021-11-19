using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels
{
        public class LayerMappedLinuxComponents 
        {
            public IEnumerable<LinuxComponent> LinuxComponents { get; set; }

            public DockerLayer DockerLayer { get; set; }
        }
}