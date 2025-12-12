namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.NuGet;

/// <summary>
/// Experiment to validate NuGetCentralPackageManagementDetector against NuGetComponentDetector.
/// </summary>
public class NuGetCentralPackageManagementDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "NuGetCentralPackageManagementDetectorExperiment";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is NuGetComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is NuGetCentralPackageManagementDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
