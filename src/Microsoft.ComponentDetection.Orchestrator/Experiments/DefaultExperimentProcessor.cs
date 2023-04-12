namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// The default experiment processor. Writes a JSON output file to a temporary directory.
/// </summary>
public class DefaultExperimentProcessor : IExperimentProcessor
{
    private readonly ILogger<DefaultExperimentProcessor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultExperimentProcessor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DefaultExperimentProcessor(ILogger<DefaultExperimentProcessor> logger) => this.logger = logger;

    /// <inheritdoc />
    public async Task ProcessExperimentAsync(IExperimentConfiguration config, ExperimentDiff diff)
    {
        var filename = Path.Combine(
            Path.GetTempPath(),
            $"Experiment_{config.Name}_{DateTime.Now:yyyyMMddHHmmssfff}_{Environment.ProcessId}.json");

        this.logger.LogInformation("Writing experiment {Name} results to {Filename}", config.Name, filename);

        await using var file = File.Create(filename);
        await JsonSerializer.SerializeAsync(file, diff, new JsonSerializerOptions { WriteIndented = true });
    }
}
