using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console.Cli;

if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"Waiting for debugger attach. PID: {Process.GetCurrentProcess().Id}");
    while (!Debugger.IsAttached)
    {
        await Task.Delay(1000);
    }
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[BOOTSTRAP] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var serviceCollection = new ServiceCollection()
    .AddComponentDetection()
    .ConfigureLoggingProviders();
using var registrar = new TypeRegistrar(serviceCollection);
var app = new CommandApp<ListDetectorsCommand>(registrar);
app.Configure(
    config =>
    {
        config.Settings.ApplicationName = "component-detection";
        config.CaseSensitivity(CaseSensitivity.None);

        config.AddCommand<ListDetectorsCommand>("list-detectors")
            .WithDescription("Lists available detectors");

        config.AddCommand<ScanCommand>("scan")
            .WithDescription("Initiates a scan");

        var resolver = registrar.Build();
        config.SetInterceptor(new Interceptor(resolver, resolver.Resolve(typeof(ILogger<Interceptor>)) as ILogger<Interceptor>));
    });
var result = await app.RunAsync(args).ConfigureAwait(false);

await Log.CloseAndFlushAsync();

return result;
