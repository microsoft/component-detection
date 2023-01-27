namespace Microsoft.ComponentDetection;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// The class containing the main entry point for Component Detection.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main entry point for Component Detection.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.ProcessExit += (not, used) =>
            {
                Console.WriteLine($"Process terminating.");
            };

            if (args.Any(x => string.Equals(x, "--Debug", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Waiting for debugger attach. PID: {Environment.ProcessId}");

                while (!Debugger.IsAttached)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }

            var orchestrator = new Orchestrator.Orchestrator();

            var result = await orchestrator.LoadAsync(args).ConfigureAwait(false);

            var exitCode = (int)result.ResultCode;
            if (result.ResultCode == ProcessingResultCode.Error || result.ResultCode == ProcessingResultCode.InputError)
            {
                exitCode = -1;
            }

            Console.WriteLine($"Execution finished, status: {exitCode}.");

            // force an exit, not letting any lingering threads not responding.
            Environment.Exit(exitCode);
        }
        catch (ArgumentException ae)
        {
            await Console.Error.WriteLineAsync(ae.ToString()).ConfigureAwait(false);
            Environment.Exit(-1);
        }
    }
}
