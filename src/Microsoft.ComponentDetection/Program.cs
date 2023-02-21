using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

try
{
    if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Waiting for debugger attach. PID: {Process.GetCurrentProcess().Id}");
        while (!Debugger.IsAttached)
        {
            await Task.Delay(1000);
        }
    }

    var providers = new LoggerProviderCollection();

    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(Path.GetTempPath(), $"GovCompDiscLog_{DateTime.Now:yyyyMMddHHmmssfff}.txt"), buffered: true)
        .WriteTo.Providers(providers)
        .MinimumLevel.ControlledBy(Orchestrator.MinimumLogLevelSwitch)
        .Enrich.FromLogContext()
        .CreateLogger();

    var serviceProvider = new ServiceCollection()
        .AddComponentDetection()
        .AddSingleton(providers)
        .AddSingleton<ILoggerFactory>(sc =>
        {
            var providerCollection = sc.GetService<LoggerProviderCollection>();
            var factory = new SerilogLoggerFactory(null, true, providerCollection);

            foreach (var provider in sc.GetServices<ILoggerProvider>())
            {
                factory.AddProvider(provider);
            }

            return factory;
        })
        .AddLogging(l => l.AddFilter<SerilogLoggerProvider>(null, LogLevel.Trace))
        .BuildServiceProvider();
    var orchestrator = serviceProvider.GetRequiredService<Orchestrator>();
    var result = await orchestrator.LoadAsync(args);

    var exitCode = (int)result.ResultCode;
    if (result.ResultCode is ProcessingResultCode.Error or ProcessingResultCode.InputError)
    {
        exitCode = -1;
    }

    Console.WriteLine($"Execution finished, status: {exitCode}.");

    await Log.CloseAndFlushAsync();

    // force an exit, not letting any lingering threads not responding.
    Environment.Exit(exitCode);
}
catch (ArgumentException ae)
{
    await Console.Error.WriteLineAsync(ae.ToString());
    Environment.Exit(-1);
}
