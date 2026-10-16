namespace Microsoft.ComponentDetection.Detectors.CondaLock;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// Experiment to validate CondaLockComponentDetector against PipReportComponentDetector.
/// </summary>
public class CondaLockDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "CondaLockDetectorExperiment";

    /// <inheritdoc />
    public Task InitAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is PipReportComponentDetector;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is CondaLockComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}
