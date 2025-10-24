#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Internal state used by PipReportDetector to hold intermediate structure info until the final
/// combination of dependencies and relationships is determined and can be returned.
/// </summary>
public sealed record PipReportGraphNode
{
    public PipReportGraphNode(PipComponent value, bool requested)
    {
        this.Value = value;
        this.Requested = requested;
    }

    public PipComponent Value { get; set; }

    public List<PipReportGraphNode> Children { get; } = [];

    public List<PipReportGraphNode> Parents { get; } = [];

    public bool Requested { get; set; }
}
