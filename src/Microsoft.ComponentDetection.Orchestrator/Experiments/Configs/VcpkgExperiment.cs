namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Vcpkg;

/// <summary>
/// Validating the <see cref="VcpkgComponentDetector"/>.
/// </summary>
public class VcpkgExperiment : IExperimentConfiguration
{
    public string Name => "VcpkgDetector";

    public bool IsInControlGroup(IComponentDetector componentDetector) => false; // There is no baseline, completely new detector

    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is VcpkgComponentDetector;

    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
