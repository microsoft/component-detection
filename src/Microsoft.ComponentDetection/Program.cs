using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Orchestrator;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Spectre.Console.Cli;

if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"Waiting for debugger attach. PID: {Process.GetCurrentProcess().Id}");
    while (!Debugger.IsAttached)
    {
        await Task.Delay(1000);
    }
}

var serviceCollection = new ServiceCollection()
    .AddComponentDetection()
    .AddLogging(l => l.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.ControlledBy(Interceptor.LogLevel)
        .Enrich.With<LoggingEnricher>()
        .Enrich.FromLogContext()
        .WriteTo.Map(
            LoggingEnricher.LogFilePathPropertyName,
            (logFilePath, wt) => wt.Async(x => x.File($"{logFilePath}")),
            1) // sinkMapCountLimit
        .WriteTo.Map<bool>(
            LoggingEnricher.PrintStderrPropertyName,
            (printLogsToStderr, wt) => wt.Logger(lc => lc
                .WriteTo.Console(standardErrorFromLevel: printLogsToStderr ? LogEventLevel.Debug : null)

                // Don't write the detection times table from DetectorProcessingService to the console, only the log file
                .Filter.ByExcluding(Matching.WithProperty<string>("DetectionTimeLine", x => !string.IsNullOrEmpty(x)))),
            1) // sinkMapCountLimit
        .CreateLogger()));

using var registrar = new TypeRegistrar(serviceCollection);
var app = new CommandApp<ListDetectorsCommand>(registrar);
app.Configure(
    config =>
    {
        var resolver = registrar.Build();

        // Create the logger here as the serviceCollection will be disposed by the time we need to use the exception handler.
        var logger = resolver.Resolve(typeof(ILogger<Program>)) as ILogger<Program>;

        config.SetInterceptor(new Interceptor(resolver));

        config.Settings.ApplicationName = "component-detection";

        config.CaseSensitivity(CaseSensitivity.None);

        config.AddCommand<ListDetectorsCommand>("list-detectors")
            .WithDescription("Lists available detectors");

        config.AddCommand<ScanCommand>("scan")
            .WithDescription("Initiates a scan");
        config.SetExceptionHandler((e, _) =>
            {
                logger.LogError(e, "An error occurred while executing the command");
            });
    });
var result = await app.RunAsync(args).ConfigureAwait(false);

await Log.CloseAndFlushAsync();

return result;
