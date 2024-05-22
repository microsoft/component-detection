namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Internal state used by PipReportDetector to hold intermediate structure info until the final
/// combination of dependencies and relationships is determined and can be returned.
/// </summary>
public class PipReportGraphNode
{
    public PipReportGraphNode(PipComponent value, bool requested)
    {
        this.Value = value;
        this.Requested = requested;
    }

    public PipComponent Value { get; set; }

    public List<PipReportGraphNode> Children { get; } = new List<PipReportGraphNode>();

    public List<PipReportGraphNode> Parents { get; } = new List<PipReportGraphNode>();

    public bool Requested { get; set; }
}
