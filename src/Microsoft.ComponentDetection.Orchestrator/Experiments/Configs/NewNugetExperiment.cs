namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.NuGet;

/// <summary>
/// Comparing the new NuGet detector approach to the old one.
/// </summary>
public class NewNugetExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "NewNugetDetector";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) =>
        componentDetector is NuGetComponentDetector or NuGetProjectModelProjectCentricComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) =>
        componentDetector is NuGetProjectModelProjectCentricComponentDetector or NuGetPackagesConfigDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
