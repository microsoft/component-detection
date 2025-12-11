namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// Experiment to validate the <see cref="LinuxApplicationLayerDetector"/> which captures application-level
/// packages in addition to system packages from Linux containers.
/// Control group includes the standard file-based npm and pip detectors plus the Linux system package detector.
/// Experiment group uses container-based detection for all package types together.
/// </summary>
public class LinuxApplicationLayerExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "LinuxApplicationLayer";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) =>
        componentDetector
            is (LinuxContainerDetector and not LinuxApplicationLayerDetector)
                or NpmComponentDetector
                or NpmLockfileDetectorBase
                or PipReportComponentDetector
                or NuGetComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) =>
        componentDetector is LinuxApplicationLayerDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents)
    {
        // Only record telemetry if the experiment group detector (LinuxApplicationLayerDetector)
        // actually found components.
        if (componentDetector is LinuxApplicationLayerDetector)
        {
            return numComponents > 0;
        }

        // For control group detectors, record if the experiment group found anything
        // This will be determined by the orchestrator based on whether the experiment group had components
        return true;
    }
}
