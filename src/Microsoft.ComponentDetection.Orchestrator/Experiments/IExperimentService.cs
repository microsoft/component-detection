namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Service for recording detector results and processing the results for any active experiments.
/// </summary>
public interface IExperimentService
{
    /// <summary>
    /// Records the results of a detector execution and processes the results for any active experiments.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <param name="components">The detected components from the <paramref name="detector"/>.</param>
    void RecordDetectorRun(IComponentDetector detector, IEnumerable<DetectedComponent> components);

    /// <summary>
    /// Called when all detectors have finished executing. Processes the experiments and reports the results.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task FinishAsync();
}
