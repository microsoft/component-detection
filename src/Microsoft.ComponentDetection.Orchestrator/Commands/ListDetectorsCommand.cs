namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Lists available detectors.
/// </summary>
public sealed class ListDetectorsCommand : Command<ListDetectorsSettings>
{
    private readonly IEnumerable<IComponentDetector> detectors;
    private readonly IAnsiConsole console;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListDetectorsCommand"/> class.
    /// </summary>
    /// <param name="detectors">The detectors.</param>
    /// <param name="console">The console.</param>
    public ListDetectorsCommand(
        IEnumerable<IComponentDetector> detectors,
        IAnsiConsole console)
    {
        this.detectors = detectors;
        this.console = console;
    }

    /// <inheritdoc/>
    public override int Execute(CommandContext context, ListDetectorsSettings settings)
    {
        var table = new Table();
        table.AddColumn("Name");

        foreach (var detector in this.detectors)
        {
            table.AddRow(detector.Id);
        }

        this.console.Write(table);

        return (int)ProcessingResultCode.Success;
    }
}
