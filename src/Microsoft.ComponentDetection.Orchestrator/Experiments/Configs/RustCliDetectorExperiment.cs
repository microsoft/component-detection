namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;

/// <summary>
/// Validating the Rust CLI detector against the Rust crate detector.
/// </summary>
public class RustCliDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "RustCliDetector";

    /// <inheritdoc/>
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is RustCrateDetector;

    /// <inheritdoc/>
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is RustCliDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
