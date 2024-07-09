namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Go;

/// <summary>
/// Validating the <see cref="GoComponentWithReplaceDetector"/>.
/// </summary>
public class GoDetectorReplaceExperiment : IExperimentConfiguration
{
    public string Name => "GoWithReplace";

    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is GoComponentDetector;

    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is GoComponentWithReplaceDetector;

    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
