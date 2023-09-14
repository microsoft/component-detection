namespace Microsoft.ComponentDetection.Orchestrator;

using System;
using System.IO;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Serilog.Core;
using Spectre.Console.Cli;

public class LogLevelInterceptor : ICommandInterceptor
{
    /// <summary>
    /// The minimum logging level to use. This will dynamically change based on the
    /// <see cref="BaseSettings.LogLevel"/> setting.
    /// </summary>
    public static readonly LoggingLevelSwitch LogLevel = new();

    /// <inheritdoc />
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is not BaseSettings baseSettings)
        {
            return;
        }

        LogLevel.MinimumLevel = baseSettings.LogLevel;
        LoggingEnricher.Path = GetLogFilePath(baseSettings.Output);
    }

    private static string GetLogFilePath(string output = null) =>
        Path.Combine(
            string.IsNullOrEmpty(output) ? Path.GetTempPath() : output,
            $"GovCompDisc_Log_{DateTime.Now:yyyyMMddHHmmssfff}_{Environment.ProcessId}.log");
}
