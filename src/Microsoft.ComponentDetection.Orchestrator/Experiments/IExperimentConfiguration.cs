namespace Microsoft.ComponentDetection.Common.Experiments;

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
}
