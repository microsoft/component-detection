namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;

/// <summary>
/// Validating the Rust SBOM detector against the Rust crate detector.
/// </summary>
public class RustSbomVsCrateExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "RustSbomVsCrateExperiment";

    /// <inheritdoc/>
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is RustCrateDetector;

    /// <inheritdoc/>
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is RustSbomDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
