namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.Extensions.Logging;
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
        if (settings is not BaseSettings baseSettings)
        {
            return;
        }

        // This is required so TelemetryRelay can be accessed via it's static singleton
        // It should be refactored out at a later date
        TelemetryRelay.Instance.Init(this.typeResolver.Resolve(typeof(IEnumerable<ITelemetryService>)) as IEnumerable<ITelemetryService>);
        TelemetryRelay.Instance.SetTelemetryMode(baseSettings.DebugTelemetry ? TelemetryMode.Debug : TelemetryMode.Production);
    }
}
