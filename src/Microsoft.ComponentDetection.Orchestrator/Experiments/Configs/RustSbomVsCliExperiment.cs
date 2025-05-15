namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;

/// <summary>
/// Validating the Rust SBOM detector against the Rust CLI detector.
/// </summary>
public class RustSbomVsCliExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "RustSbomVsCliExperiment";

    /// <inheritdoc/>
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is RustCliDetector;

    /// <inheritdoc/>
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is RustSbomDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
