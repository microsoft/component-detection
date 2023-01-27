namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Internal state used by PipDetector to hold intermediate structure info until the final
/// combination of dependencies and relationships is determined and can be returned.
/// </summary>
public class PipGraphNode
{
    public PipGraphNode(PipComponent value) => this.Value = value;

    public PipComponent Value { get; set; }

    public IList<PipGraphNode> Children { get; } = new List<PipGraphNode>();

    public IList<PipGraphNode> Parents { get; } = new List<PipGraphNode>();
}
