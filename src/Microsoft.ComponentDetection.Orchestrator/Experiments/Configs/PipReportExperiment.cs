namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// Validating the <see cref="PipReportComponentDetector"/>.
/// </summary>
public class PipReportExperiment : IExperimentConfiguration
{
    public string Name => "PipReport";

    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is PipComponentDetector;

    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is PipReportComponentDetector;

    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
