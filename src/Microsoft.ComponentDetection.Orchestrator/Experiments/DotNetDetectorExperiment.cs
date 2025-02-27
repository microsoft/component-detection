namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.DotNet;

/// <summary>
/// Validating the <see cref="DotNetDetectorExperiment"/>.
/// </summary>
public class DotNetDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "DotNetDetector";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => false;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is DotNetComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
