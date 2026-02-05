namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Maven;

/// <summary>
/// Experiment to validate MavenWithFallbackDetector against MvnCliComponentDetector.
/// The MavenWithFallbackDetector combines MvnCli detection with static pom.xml parsing fallback
/// for cases where Maven CLI fails (e.g., authentication errors with private feeds).
/// </summary>
public class MavenWithFallbackExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "MavenWithFallback";

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is MvnCliComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is MavenWithFallbackDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => numComponents > 0;
}
