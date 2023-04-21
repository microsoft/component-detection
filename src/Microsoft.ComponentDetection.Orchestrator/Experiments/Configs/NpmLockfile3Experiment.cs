namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Npm;

/// <summary>
/// Validating the <see cref="NpmLockfile3Detector"/>.
/// </summary>
public class NpmLockfile3Experiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "LockfileVersion3";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is NpmComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is NpmLockfile3Detector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) =>
        componentDetector is not NpmComponentDetector || numComponents == 0;
}
