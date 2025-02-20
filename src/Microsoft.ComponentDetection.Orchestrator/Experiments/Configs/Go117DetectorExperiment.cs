namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Go;

/// <summary>
/// Validating the Go detector for go mod 1.17+.
/// </summary>
public class Go117DetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "Go117Detector";

    /// <inheritdoc/>
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is GoComponentDetector;

    /// <inheritdoc/>
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is Go117ComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
