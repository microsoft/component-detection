namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.DotNet;
using Microsoft.ComponentDetection.Detectors.NuGet;

/// <summary>
/// Experiment configuration for validating the <see cref="MSBuildBinaryLogComponentDetector"/>
/// against the existing <see cref="NuGetProjectModelProjectCentricComponentDetector"/> and
/// <see cref="DotNetComponentDetector"/>.
/// </summary>
public class MSBuildBinaryLogExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "MSBuildBinaryLogDetector";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) =>
        componentDetector is NuGetProjectModelProjectCentricComponentDetector or DotNetComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) =>
        componentDetector is MSBuildBinaryLogComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
