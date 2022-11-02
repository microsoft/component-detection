namespace Microsoft.ComponentDetection
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.ComponentDetection.Contracts;

    public class Program
    {
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
                    Console.WriteLine($"Waiting for debugger attach. PID: {Process.GetCurrentProcess().Id}");

                    while (!Debugger.IsAttached)
                    {
                        await Task.Delay(1000);
                    }
                }

                var orchestrator = new Orchestrator.Orchestrator();

                var result = await orchestrator.LoadAsync(args);

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
                await Console.Error.WriteLineAsync(ae.ToString());
                Environment.Exit(-1);
            }
        }
    }
}
