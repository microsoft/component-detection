namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Composer;

/// <summary>
/// Validates the <see cref="ComposerComponentDetector"/> against an empty baseline because there is no existing Composer detector.
/// </summary>
public class ComposerExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "Composer";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => false;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is ComposerComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
