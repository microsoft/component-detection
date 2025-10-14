namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Common.Telemetry;
using Serilog.Core;
using Spectre.Console.Cli;

/// <summary>
/// Intercepts all commands before they are executed.
/// </summary>
public class Interceptor : ICommandInterceptor
{
    /// <summary>
    /// The minimum logging level to use. This will dynamically change based on the
    /// <see cref="BaseSettings.LogLevel"/> setting.
    /// </summary>
    public static readonly LoggingLevelSwitch LogLevel = new();

    private readonly ITypeResolver typeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="Interceptor"/> class.
    /// </summary>
    /// <param name="typeResolver">The type resolver.</param>
    public Interceptor(ITypeResolver typeResolver) => this.typeResolver = typeResolver;

    /// <inheritdoc />
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is BaseSettings baseSettings)
        {
            LogLevel.MinimumLevel = baseSettings.LogLevel;
            LoggingEnricher.Path = GetLogFilePath(baseSettings.Output);

            // This is required so TelemetryRelay can be accessed via it's static singleton
            // It should be refactored out at a later date
            TelemetryRelay.Instance.Init(this.typeResolver.Resolve(typeof(IEnumerable<ITelemetryService>)) as IEnumerable<ITelemetryService>);
            TelemetryRelay.Instance.SetTelemetryMode(baseSettings.DebugTelemetry ? TelemetryMode.Debug : TelemetryMode.Production);
        }

        if (settings is ScanSettings scanSettings)
        {
            LoggingEnricher.PrintStderr = scanSettings.PrintManifest;
        }
    }

    private static string GetLogFilePath(string output = null) =>
        Path.Combine(
            string.IsNullOrEmpty(output) ? Path.GetTempPath() : output,
            $"GovCompDisc_Log_{DateTime.Now:yyyyMMddHHmmssfff}_{Environment.ProcessId}.log");
}
