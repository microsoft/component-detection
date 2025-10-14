#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Defines the configuration for an experiment. An experiment is a set of detectors that are grouped into control and
/// experiment groups. The control group is used to determine the baseline for the experiment. The experiment group is
/// used to determine the impact of the experiment on the baseline. The unique set of components from the two sets of
/// detectors is compared and differences are reported via telemetry.
/// </summary>
public interface IExperimentConfiguration
{
    /// <summary>
    /// The name of the experiment.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes the experiment configuration.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task InitAsync() => Task.CompletedTask;

    /// <summary>
    /// Specifies if the detector is in the control group.
    /// </summary>
    /// <param name="componentDetector">The detector.</param>
    /// <returns><c>true</c> if the detector is in the control group; otherwise, <c>false</c>.</returns>
    bool IsInControlGroup(IComponentDetector componentDetector);

    /// <summary>
    /// Specifies if the detector is in the control group.
    /// </summary>
    /// <param name="componentDetector">The detector.</param>
    /// <returns><c>true</c> if the detector is in the experiment group; otherwise, <c>false</c>.</returns>
    bool IsInExperimentGroup(IComponentDetector componentDetector);

    /// <summary>
    /// Determines if the experiment should be recorded, given a list of all the detectors that ran and the number of
    /// components that were detected. If any call to this method returns <c>false</c>, the experiment will not be
    /// recorded.
    /// </summary>
    /// <param name="componentDetector">The component detector.</param>
    /// <param name="numComponents">The number of components found by the <paramref name="componentDetector"/>.</param>
    /// <returns><c>true</c> if the experiment should be recorded; otherwise, <c>false</c>.</returns>
    bool ShouldRecord(IComponentDetector componentDetector, int numComponents);
}
