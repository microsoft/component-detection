#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator;

using System;

public class DetectorRunResult
{
    public TimeSpan ExecutionTime { get; set; }

    public int ComponentsFoundCount { get; set; }

    public int ExplicitlyReferencedComponentCount { get; set; }

    public bool IsExperimental { get; set; }
}
