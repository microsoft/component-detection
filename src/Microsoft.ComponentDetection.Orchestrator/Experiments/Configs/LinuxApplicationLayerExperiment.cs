namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// Experiment to validate the <see cref="LinuxApplicationLayerDetector"/> which captures application-level
/// packages in addition to system packages from Linux containers.
/// Control group uses file-based detectors plus LinuxContainerDetector (system packages only).
/// Experiment group uses file-based detectors plus LinuxApplicationLayerDetector (system + application packages).
/// The diff reveals net-new application packages found only inside containers (e.g., RUN npm add lodash),
/// excluding both manifest-detected components (canceled by file-based detectors) and system packages
/// (canceled by LinuxContainerDetector).
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
                or NuGetComponentDetector
                or NuGetProjectModelProjectCentricComponentDetector
                or NuGetPackagesConfigDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) =>
        componentDetector
            is LinuxApplicationLayerDetector
                or NpmComponentDetector
                or NpmLockfileDetectorBase
                or PipReportComponentDetector
                or NuGetComponentDetector
                or NuGetProjectModelProjectCentricComponentDetector
                or NuGetPackagesConfigDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents)
    {
        // Only record telemetry if a Linux container detector found components,
        // indicating containers were detected and scanned.
        if (componentDetector is LinuxContainerDetector)
        {
            return numComponents > 0;
        }

        return true;
    }
}
