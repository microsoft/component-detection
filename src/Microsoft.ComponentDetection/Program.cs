using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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

    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    var serviceProvider = new ServiceCollection()
        .AddComponentDetection()
        .ConfigureLoggingProviders()
        .BuildServiceProvider();
    var orchestrator = serviceProvider.GetRequiredService<Orchestrator>();
    var result = await orchestrator.LoadAsync(args);

    var exitCode = (int)result.ResultCode;
    if (result.ResultCode is ProcessingResultCode.Error or ProcessingResultCode.InputError)
    {
        exitCode = -1;
    }

    Console.WriteLine($"Execution finished, status: {exitCode}.");

    // Manually dispose to flush logs as we force exit
    await serviceProvider.DisposeAsync();

    // force an exit, not letting any lingering threads not responding.
    Environment.Exit(exitCode);
}
catch (ArgumentException ae)
{
    await Console.Error.WriteLineAsync(ae.ToString());
    Environment.Exit(-1);
}
