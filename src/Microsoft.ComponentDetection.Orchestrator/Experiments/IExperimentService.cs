namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Commands;

/// <summary>
/// Service for recording detector results and processing the results for any active experiments.
/// </summary>
public interface IExperimentService
{
    /// <summary>
    /// Records the results of a detector execution and processes the results for any active experiments.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <param name="componentRecorder">The detected components from the <paramref name="detector"/>.</param>
    /// <param name="settings">The detection settings.</param>
    void RecordDetectorRun(IComponentDetector detector, ComponentRecorder componentRecorder, ScanSettings settings);

    /// <summary>
    /// Called when all detectors have finished executing. Processes the experiments and reports the results.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task FinishAsync();

    /// <summary>
    /// Removes any experimentsthat contains a detector that is not needed.
    /// </summary>
    /// <param name="detectors"> List of all detectors. </param>
    void RemoveUnwantedExperimentsbyDetectors(IEnumerable<IComponentDetector> detectors);
}
