namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
public class ExperimentService : IExperimentService
{
    private readonly ConcurrentDictionary<IExperimentConfiguration, ExperimentResults> experiments;
    private readonly IEnumerable<IExperimentProcessor> experimentProcessors;
    private readonly IGraphTranslationService graphTranslationService;
    private readonly ILogger<ExperimentService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentService"/> class.
    /// </summary>
    /// <param name="configs">The experiment configurations.</param>
    /// <param name="experimentProcessors">The experiment processors.</param>
    /// <param name="graphTranslationService">The graph translation service.</param>
    /// <param name="logger">The logger.</param>
    public ExperimentService(
        IEnumerable<IExperimentConfiguration> configs,
        IEnumerable<IExperimentProcessor> experimentProcessors,
        IGraphTranslationService graphTranslationService,
        ILogger<ExperimentService> logger)
    {
        this.experiments = new ConcurrentDictionary<IExperimentConfiguration, ExperimentResults>(
            configs.ToDictionary(config => config, _ => new ExperimentResults()));
        this.experimentProcessors = experimentProcessors;
        this.graphTranslationService = graphTranslationService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        foreach (var config in this.experiments.Keys)
        {
            try
            {
                await config.InitAsync();
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Failed to initialize experiment {Experiment}, skipping it", config.Name);
                this.experiments.TryRemove(config, out _);
            }
        }
    }

    /// <inheritdoc />
    public void RecordDetectorRun(
        IComponentDetector detector,
        ComponentRecorder componentRecorder,
        ScanSettings settings,
        DetectorRunResult detectorRunResult = null)
    {
        if (!DetectorExperiments.AreExperimentsEnabled)
        {
            return;
        }

        try
        {
            var scanResult = this.graphTranslationService.GenerateScanResultFromProcessingResult(
                new DetectorProcessingResult()
                {
                    ComponentRecorders = [(detector, componentRecorder)],
                    ContainersDetailsMap = [],
                    ResultCode = ProcessingResultCode.Success,
                },
                settings,
                false);

            var components = scanResult.ComponentsFound;
            this.FilterExperiments(detector, components.Count());

            foreach (var (config, experimentResults) in this.experiments)
            {
                if (config.IsInControlGroup(detector))
                {
                    experimentResults.AddComponentsToControlGroup(components);

                    experimentResults.AddControlDetectorTime(detector.Id, detectorRunResult?.ExecutionTime ?? TimeSpan.Zero);
                    this.logger.LogDebug(
                        "Adding {Count} Components from {Id} to Control Group for {Experiment}",
                        components.Count(),
                        detector.Id,
                        config.Name);
                }

                if (config.IsInExperimentGroup(detector))
                {
                    experimentResults.AddComponentsToExperimentalGroup(components);

                    experimentResults.AddExperimentalDetectorTime(detector.Id, detectorRunResult?.ExecutionTime ?? TimeSpan.Zero);
                    this.logger.LogDebug(
                        "Adding {Count} Components from {Id} to Experiment Group for {Experiment}",
                        components.Count(),
                        detector.Id,
                        config.Name);
                }

                if (detector is FileComponentDetector fileDetector)
                {
                    experimentResults.AddAdditionalPropertiesToExperiment(fileDetector.AdditionalProperties);
                }
            }
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to record detector run");
        }
    }

    private void FilterExperiments(IComponentDetector detector, int count)
    {
        var experimentsToRemove = this.experiments
            .Where(x => !x.Key.ShouldRecord(detector, count))
            .Select(x => x.Key)
            .ToList();

        foreach (var config in experimentsToRemove.Where(config => this.experiments.TryRemove(config, out _)))
        {
            this.logger.LogDebug("Removing {Experiment} from active experiments", config.Name);
        }
    }

    public void RemoveUnwantedExperimentsbyDetectors(IEnumerable<IComponentDetector> detectors)
    {
        if (detectors == null)
        {
            return;
        }

        var experimentsToRemove = this.experiments
            .Where(x => detectors.Any(detector => x.Key.IsInControlGroup(detector) || x.Key.IsInExperimentGroup(detector)))
            .Select(x => x.Key).ToList();

        foreach (var config in experimentsToRemove.Where(config => this.experiments.TryRemove(config, out _)))
        {
            this.logger.LogDebug("Removing {Experiment} from active experiments", config.Name);
        }
    }

    /// <inheritdoc />
    public async Task FinishAsync()
    {
        if (!DetectorExperiments.AreExperimentsEnabled)
        {
            return;
        }

        if (!DetectorExperiments.AutomaticallyProcessExperiments)
        {
            return;
        }

        foreach (var (config, experiment) in this.experiments)
        {
            var controlComponents = experiment.ControlGroupComponents;
            var experimentComponents = experiment.ExperimentGroupComponents;
            var controlDetectors = experiment.ControlDetectors;
            var experimentDetectors = experiment.ExperimentalDetectors;
            var additionalProperties = experiment.AdditionalProperties;
            this.logger.LogInformation(
                "Experiment {Experiment} finished with {ControlCount} components in the control group and {ExperimentCount} components in the experiment group",
                config.Name,
                controlComponents.Count,
                experimentComponents.Count);

            // If there are no components recorded in the experiment, skip processing experiments. We still want to
            // process empty diffs as this means the experiment was successful.
            if (!experimentComponents.Any() && !controlComponents.Any())
            {
                this.logger.LogInformation("Experiment {Experiment} has no components in either group, skipping processing", config.Name);
                continue;
            }

            try
            {
                var diff = new ExperimentDiff(controlComponents, experimentComponents, controlDetectors, experimentDetectors, additionalProperties);
                var tasks = this.experimentProcessors.Select(x => x.ProcessExperimentAsync(config, diff));
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Error processing experiment {Experiment}", config.Name);
            }
        }
    }
}
