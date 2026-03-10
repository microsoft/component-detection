namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Uv;

/// <summary>
/// Experiment to validate UvLockComponentDetector against PipComponentDetector.
/// </summary>
public class UvLockDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "UvLockDetectorExperiment";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is PipReportComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is UvLockComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
