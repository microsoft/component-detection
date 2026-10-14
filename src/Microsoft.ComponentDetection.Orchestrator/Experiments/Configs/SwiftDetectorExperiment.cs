namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Swift;

/// <summary>
/// Records Swift Package Manager discoveries against an empty baseline.
/// </summary>
public class SwiftDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "SwiftDetectorExperiment";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => false;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is SwiftResolvedComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
