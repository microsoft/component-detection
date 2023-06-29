namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Extensions.Logging;
using Spectre.Console.Cli;

/// <summary>
/// Intercepts all commands before they are executed.
/// </summary>
public class Interceptor : ICommandInterceptor
{
    private readonly ITypeResolver typeResolver;
    private readonly ILogger<Interceptor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Interceptor"/> class.
    /// </summary>
    /// <param name="typeResolver">The type resolver.</param>
    /// <param name="logger">The logger.</param>
    public Interceptor(ITypeResolver typeResolver, ILogger<Interceptor> logger)
    {
        this.typeResolver = typeResolver;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is BaseSettings baseSettings)
        {
            this.ConfigureLogger(baseSettings);

            // This is required so TelemetryRelay can be accessed via it's static singleton
            // It should be refactored out at a later date
            TelemetryRelay.Instance.Init(this.typeResolver.Resolve(typeof(IEnumerable<ITelemetryService>)) as IEnumerable<ITelemetryService>);
            TelemetryRelay.Instance.SetTelemetryMode(baseSettings.DebugTelemetry ? TelemetryMode.Debug : TelemetryMode.Production);
        }
    }

    private void ConfigureLogger(BaseSettings settings)
    {
        var logFile = Path.Combine(
            settings.Output ?? Path.GetTempPath(),
            $"GovCompDisc_Log_{DateTime.Now:yyyyMMddHHmmssfff}_{Environment.ProcessId}.log");

        var reloadableLogger = (ReloadableLogger)Log.Logger;
        reloadableLogger.Reload(configuration =>
            configuration
                .WriteTo.Console(standardErrorFromLevel: settings is ScanSettings { PrintManifest: true } ? LogEventLevel.Debug : null)
                .WriteTo.Async(x => x.File(logFile))
                .WriteTo.Providers(this.typeResolver.Resolve(typeof(LoggerProviderCollection)) as LoggerProviderCollection)
                .MinimumLevel.Is(settings.Verbosity switch
                {
                    VerbosityMode.Quiet => LogEventLevel.Error,
                    VerbosityMode.Normal => LogEventLevel.Information,
                    VerbosityMode.Verbose => LogEventLevel.Debug,
                    _ => throw new ArgumentOutOfRangeException(nameof(settings.Verbosity), "Invalid verbosity level"),
                })
                .Enrich.FromLogContext());

        this.logger.LogInformation("Log file: {LogFile}", logFile);
    }
}
