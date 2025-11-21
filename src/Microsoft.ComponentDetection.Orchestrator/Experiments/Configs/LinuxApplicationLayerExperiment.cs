namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux;

/// <summary>
/// Experiment to validate the <see cref="LinuxApplicationLayerDetector"/> which captures application-level
/// packages in addition to system packages from Linux containers.
/// </summary>
public class LinuxApplicationLayerExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "LinuxApplicationLayer";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) =>
        componentDetector is LinuxContainerDetector and not LinuxApplicationLayerDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) =>
        componentDetector is LinuxApplicationLayerDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
