namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pnpm;

/// <summary>
/// Validating the <see cref="Pnpm6ComponentDetector"/>.
/// </summary>
public class Pnpm6Experiment : IExperimentConfiguration
{
    public string Name => "Pnpm6";

    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is PnpmComponentDetector;

    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is Pnpm6ComponentDetector;

    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
