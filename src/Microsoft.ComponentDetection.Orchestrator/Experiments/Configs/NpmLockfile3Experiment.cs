namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Pnpm;
using Microsoft.ComponentDetection.Detectors.Poetry;
using Microsoft.ComponentDetection.Detectors.Yarn;

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
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents)
    {
        if (numComponents == 0)
        {
            return true;
        }

        return componentDetector switch
        {
            NpmComponentDetector
                or NpmComponentDetectorWithRoots
                or PnpmComponentDetector
                or PoetryComponentDetector
                or YarnLockComponentDetector => false,
            _ => true,
        };
    }
}
