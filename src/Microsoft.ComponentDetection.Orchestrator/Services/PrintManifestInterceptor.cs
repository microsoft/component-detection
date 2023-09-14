namespace Microsoft.ComponentDetection.Orchestrator.Services;

using Microsoft.ComponentDetection.Orchestrator.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Intercepts the <see cref="ScanSettings.PrintManifest"/> setting and sets the <see cref="LoggingEnricher.PrintStderr"/> property accordingly.
/// </summary>
public class PrintManifestInterceptor : ICommandInterceptor
{
    /// <inheritdoc />
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is not ScanSettings scanSettings)
        {
            return;
        }

        LoggingEnricher.PrintStderr = scanSettings.PrintManifest;
    }
}
