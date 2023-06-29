namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

/// <summary>
/// Lists available detectors.
/// </summary>
public sealed class ListDetectorsCommand : Command<ListDetectorsSettings>
{
    private readonly IEnumerable<IComponentDetector> detectors;
    private readonly ILogger<ListDetectorsCommand> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListDetectorsCommand"/> class.
    /// </summary>
    /// <param name="detectors">The detectors.</param>
    /// <param name="logger">The logger.</param>
    public ListDetectorsCommand(
        IEnumerable<IComponentDetector> detectors,
        ILogger<ListDetectorsCommand> logger)
    {
        this.detectors = detectors;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public override int Execute(CommandContext context, ListDetectorsSettings settings)
    {
        this.logger.LogInformation("Detectors:");

        foreach (var detector in this.detectors)
        {
            this.logger.LogInformation("{DetectorId}", detector.Id);
        }

        return (int)ProcessingResultCode.Success;
    }
}
