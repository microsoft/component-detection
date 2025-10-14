#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

/// <summary>
/// Processes the results of an experiment. Used to report the results of an experiment, such as by writing to a file.
/// </summary>
public interface IExperimentProcessor
{
    /// <summary>
    /// Asynchronously processes the results of an experiment.
    /// </summary>
    /// <param name="config">The experiment configuration.</param>
    /// <param name="diff">The difference in components between two sets of detectors.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ProcessExperimentAsync(IExperimentConfiguration config, ExperimentDiff diff);
}
