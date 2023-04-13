namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
public class ExperimentService : IExperimentService
{
    private readonly List<(IExperimentConfiguration Config, ExperimentResults ExperimentResults)> experiments;
    private readonly IEnumerable<IExperimentProcessor> experimentProcessors;
    private readonly ILogger<ExperimentService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentService"/> class.
    /// </summary>
    /// <param name="configs">The experiment configurations.</param>
    /// <param name="experimentProcessors">The experiment processors.</param>
    /// <param name="logger">The logger.</param>
    public ExperimentService(
        IEnumerable<IExperimentConfiguration> configs,
        IEnumerable<IExperimentProcessor> experimentProcessors,
        ILogger<ExperimentService> logger)
    {
        this.experiments = configs.Select(x => (x, new ExperimentResults())).ToList();
        this.experimentProcessors = experimentProcessors;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void RecordDetectorRun(IComponentDetector detector, IEnumerable<DetectedComponent> components)
    {
        foreach (var (config, experimentResults) in this.experiments)
        {
            if (config.IsInControlGroup(detector))
            {
                experimentResults.AddComponentsToControlGroup(components);
                this.logger.LogDebug(
                    "Adding {Count} Components from {Id} to Control Group for {Experiment}",
                    components.Count(),
                    detector.Id,
                    config.Name);
            }

            if (config.IsInExperimentGroup(detector))
            {
                experimentResults.AddComponentsToExperimentalGroup(components);
                this.logger.LogDebug(
                    "Adding {Count} Components from {Id} to Experiment Group for {Experiment}",
                    components.Count(),
                    detector.Id,
                    config.Name);
            }
        }
    }

    /// <inheritdoc />
    public async Task FinishAsync()
    {
        foreach (var (config, experiment) in this.experiments)
        {
            var oldComponents = experiment.ControlGroupComponents;
            var newComponents = experiment.ExperimentGroupComponents;

            this.logger.LogInformation(
                "Experiment {Experiment} finished and has {Count} components in the control group and {Count} components in the experiment group.",
                config.Name,
                oldComponents.Count,
                newComponents.Count);

            var diff = new ExperimentDiff(experiment.ControlGroupComponents, experiment.ExperimentGroupComponents);

            foreach (var processor in this.experimentProcessors)
            {
                await processor.ProcessExperimentAsync(config, diff);
            }
        }
    }
}
