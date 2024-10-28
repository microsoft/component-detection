namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// Validating the <see cref="SimplePipComponentDetector"/>.
/// </summary>
public class NuGetTargetFrameworkExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "NuGetTargetFrameworkAwareDetector";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is NuGetProjectModelProjectCentricComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is NuGetPackageReferenceFrameworkAwareDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
