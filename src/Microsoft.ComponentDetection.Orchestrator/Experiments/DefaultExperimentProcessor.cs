namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// The default experiment processor. Writes a JSON output file to a temporary directory.
/// </summary>
public class DefaultExperimentProcessor : IExperimentProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    private readonly IFileWritingService fileWritingService;
    private readonly ILogger<DefaultExperimentProcessor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultExperimentProcessor"/> class.
    /// </summary>
    /// <param name="fileWritingService">The file writing service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultExperimentProcessor(IFileWritingService fileWritingService, ILogger<DefaultExperimentProcessor> logger)
    {
        this.fileWritingService = fileWritingService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessExperimentAsync(IExperimentConfiguration config, ExperimentDiff diff)
    {
        var filename = $"Experiment_{config.Name}_{{timestamp}}_{Environment.ProcessId}.json";

        this.logger.LogInformation("Writing experiment {Name} results to {Filename}", config.Name, this.fileWritingService.ResolveFilePath(filename));

        var serializedDiff = JsonSerializer.Serialize(diff, JsonOptions);
        await this.fileWritingService.WriteFileAsync(filename, serializedDiff);
    }
}
