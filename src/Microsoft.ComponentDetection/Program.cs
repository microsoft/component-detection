using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator;

namespace Microsoft.ComponentDetection.Loader
{
    public class Program
    {
        public static void Main(string[] args)
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
                        System.Threading.Tasks.Task.Delay(1000).GetAwaiter().GetResult();
                    }
                }

                var orchestrator = new Orchestrator.Orchestrator();

                var result = orchestrator.Load(args);

                int exitCode = (int)result.ResultCode;
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
                Console.Error.WriteLine(ae.ToString());
                Environment.Exit(-1);
            }
        }
    }
}
