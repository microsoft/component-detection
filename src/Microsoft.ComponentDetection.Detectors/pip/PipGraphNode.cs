using System.Collections.ObjectModel;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Detectors.Pip
{
    /// <summary>
    /// Internal state used by PipDetector to hold intermediate structure info until the final
    /// combination of dependencies and relationships is determined and can be returned.
    /// </summary>
    public class PipGraphNode
    {
        public PipGraphNode(PipComponent value)
        {
            this.Value = value;
        }

        public PipComponent Value { get; set; }

        public Collection<PipGraphNode> Children { get; } = new Collection<PipGraphNode>();

        public Collection<PipGraphNode> Parents { get; } = new Collection<PipGraphNode>();
    }
}
