namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using System.Threading;
using Microsoft.ComponentDetection.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Lists available detectors.
/// </summary>
/// <param name="detectors">The detectors.</param>
/// <param name="console">The console.</param>
public sealed class ListDetectorsCommand(
    IEnumerable<IComponentDetector> detectors,
    IAnsiConsole console) : Command<ListDetectorsSettings>
{
    /// <inheritdoc/>
    public override int Execute(
        CommandContext context,
        ListDetectorsSettings settings,
        CancellationToken cancellationToken)
    {
        var table = new Table();
        table.AddColumn("Name");

        foreach (var detector in detectors)
        {
            table.AddRow(detector.Id);
        }

        console.Write(table);

        return (int)ProcessingResultCode.Success;
    }
}
