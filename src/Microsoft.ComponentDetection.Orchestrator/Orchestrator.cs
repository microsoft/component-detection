namespace Microsoft.ComponentDetection.Orchestrator;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Exceptions;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Newtonsoft.Json;

public class Orchestrator
{
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    [ImportMany]
    private static IEnumerable<IArgumentHandlingService> ArgumentHandlers { get; set; }

    [Import]
    private static Logger Logger { get; set; }

    [Import]
    private static FileWritingService FileWritingService { get; set; }

    [Import]
    private static IArgumentHelper ArgumentHelper { get; set; }

    public async Task<ScanResult> LoadAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var argumentHelper = new ArgumentHelper { ArgumentSets = new[] { new BaseArguments() } };
        BaseArguments baseArguments = null;
        var parserResult = argumentHelper.ParseArguments<BaseArguments>(args, true);
        parserResult.WithParsed(x => baseArguments = x);
        if (parserResult.Tag == ParserResultType.NotParsed)
        {
            // Blank args for this part of the loader, all things are optional and default to false / empty / null
            baseArguments = new BaseArguments();
        }

        var additionalDITargets = baseArguments.AdditionalDITargets ?? Enumerable.Empty<string>();

        // Load all types from Common (where Logger lives) and our executing assembly.
        var configuration = new ContainerConfiguration()
            .WithAssembly(typeof(Logger).Assembly)
            .WithAssembly(Assembly.GetExecutingAssembly());

        foreach (var assemblyPath in additionalDITargets)
        {
            var assemblies = Assembly.LoadFrom(assemblyPath);

            AddAssembliesWithType<ITelemetryService>(assemblies, configuration);
            AddAssembliesWithType<IGraphTranslationService>(assemblies, configuration);
        }

        using (var container = configuration.CreateContainer())
        {
            container.SatisfyImports(this);
            container.SatisfyImports(TelemetryRelay.Instance);
        }

        TelemetryRelay.Instance.SetTelemetryMode(baseArguments.DebugTelemetry ? TelemetryMode.Debug : TelemetryMode.Production);

        var shouldFailureBeSuppressed = false;

        // Don't use the using pattern here so we can take care not to clobber the stack
        var returnResult = await BcdeExecutionTelemetryRecord.TrackAsync(
            async (record, ct) =>
            {
                var executionResult = await this.HandleCommandAsync(args, record, ct);
                if (executionResult.ResultCode == ProcessingResultCode.PartialSuccess)
                {
                    shouldFailureBeSuppressed = true;
                    record.HiddenExitCode = (int)executionResult.ResultCode;
                }

                return executionResult;
            },
            true,
            cancellationToken);

        // The order of these things is a little weird, but done this way mostly to prevent any of the logic inside if blocks from being duplicated
        if (shouldFailureBeSuppressed)
        {
            Logger.LogInfo("The scan had some detections complete while others encountered errors. The log file should indicate any issues that happened during the scan.");
        }

        if (returnResult.ResultCode == ProcessingResultCode.TimeoutError)
        {
            // If we have a timeout we need to tear the run down as a CYA -- this expected to fix the problem of not responding detection runs (e.g. really long runs that don't terminate when the timeout is reached).
            // Current suspicion is that we're able to get to this point in the code without child processes cleanly cleaned up.
            // Observation also shows that doing this is terminating the process significantly more quickly in local executions.
            Environment.Exit(shouldFailureBeSuppressed ? 0 : (int)returnResult.ResultCode);
        }

        if (shouldFailureBeSuppressed)
        {
            returnResult.ResultCode = ProcessingResultCode.Success;
        }

        // We should not have input errors at this point, return it as an Error
        if (returnResult.ResultCode == ProcessingResultCode.InputError)
        {
            returnResult.ResultCode = ProcessingResultCode.Error;
        }

        return returnResult;
    }

    private static void AddAssembliesWithType<T>(Assembly assembly, ContainerConfiguration containerConfiguration) => assembly.GetTypes()
            .Where(x => typeof(T).IsAssignableFrom(x)).ToList()
            .ForEach(service => containerConfiguration = containerConfiguration.WithPart(service));

    public async Task<ScanResult> HandleCommandAsync(
        string[] args,
        BcdeExecutionTelemetryRecord telemetryRecord,
        CancellationToken cancellationToken = default)
    {
        var scanResult = new ScanResult()
        {
            ResultCode = ProcessingResultCode.Error,
        };

        var parsedArguments = ArgumentHelper.ParseArguments(args);
        await parsedArguments.WithParsedAsync<IScanArguments>(async argumentSet =>
        {
            CommandLineArgumentsExporter.ArgumentsForDelayedInjection = argumentSet;

            // Don't set production telemetry if we are running the build task in DevFabric. 0.36.0 is set in the task.json for the build task in development, but is calculated during deployment for production.
            TelemetryConstants.CorrelationId = argumentSet.CorrelationId;
            telemetryRecord.Command = this.GetVerb(argumentSet);

            scanResult = await this.SafelyExecuteAsync(telemetryRecord, async () =>
            {
                await this.GenerateEnvironmentSpecificTelemetryAsync(telemetryRecord);

                telemetryRecord.Arguments = JsonConvert.SerializeObject(argumentSet);
                FileWritingService.Init(argumentSet.Output);
                Logger.Init(argumentSet.Verbosity, writeLinePrefix: true);
                Logger.LogInfo($"Run correlation id: {TelemetryConstants.CorrelationId}");

                return await this.DispatchAsync(argumentSet, cancellationToken);
            });
        });
        parsedArguments.WithNotParsed(errors =>
        {
            if (errors.Any(e => e is HelpVerbRequestedError))
            {
                telemetryRecord.Command = "help";
                scanResult.ResultCode = ProcessingResultCode.Success;
            }
        });

        if (parsedArguments.Tag == ParserResultType.NotParsed)
        {
            // If the parsing failed, we already outputted an error.
            // so just quit.
            return scanResult;
        }

        telemetryRecord.ExitCode = (int)scanResult.ResultCode;
        return scanResult;
    }

    private async Task GenerateEnvironmentSpecificTelemetryAsync(BcdeExecutionTelemetryRecord telemetryRecord)
    {
        telemetryRecord.AgentOSDescription = RuntimeInformation.OSDescription;

        if (IsLinux && RuntimeInformation.OSDescription.Contains("Ubuntu", StringComparison.InvariantCultureIgnoreCase))
        {
            const string LibSslDetailsKey = "LibSslDetailsKey";
            var agentOSMeaningfulDetails = new Dictionary<string, string> { { LibSslDetailsKey, "FailedToFetch" } };
            var taskTimeout = TimeSpan.FromSeconds(20);

            try
            {
                var getLibSslPackages = Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo("apt", "list --installed") { RedirectStandardOutput = true };
                    var process = new Process { StartInfo = startInfo };
                    process.Start();
                    string aptListResult = null;
                    var task = Task.Run(() => aptListResult = process.StandardOutput.ReadToEnd());
                    task.Wait();
                    process.WaitForExit();

                    return string.Join(Environment.NewLine, aptListResult.Split(Environment.NewLine).Where(x => x.Contains("libssl")));
                });
                await getLibSslPackages.WaitAsync(taskTimeout);

                agentOSMeaningfulDetails[LibSslDetailsKey] = await getLibSslPackages;
            }
            catch (Exception ex)
            {
                agentOSMeaningfulDetails[LibSslDetailsKey] += Environment.NewLine + ex.ToString();
            }
            finally
            {
                telemetryRecord.AgentOSMeaningfulDetails = JsonConvert.SerializeObject(agentOSMeaningfulDetails);
            }
        }
    }

    private string GetVerb(IScanArguments argumentSet)
    {
        var verbAttribute = argumentSet.GetType().GetCustomAttribute<VerbAttribute>();
        return verbAttribute.Name;
    }

    private async Task<ScanResult> DispatchAsync(IScanArguments arguments, CancellationToken cancellation)
    {
        var scanResult = new ScanResult()
        {
            ResultCode = ProcessingResultCode.Error,
        };

        if (ArgumentHandlers == null)
        {
            Logger.LogError("No argument handling services were registered.");
            return scanResult;
        }

        foreach (var handler in ArgumentHandlers)
        {
            if (handler.CanHandle(arguments))
            {
                try
                {
                    var timeout = arguments.Timeout == 0 ? TimeSpan.FromMilliseconds(-1) : TimeSpan.FromSeconds(arguments.Timeout);
                    scanResult = await AsyncExecution.ExecuteWithTimeoutAsync(() => handler.HandleAsync(arguments), timeout, cancellation);
                }
                catch (TimeoutException timeoutException)
                {
                    Logger.LogError(timeoutException.Message);
                    scanResult.ResultCode = ProcessingResultCode.TimeoutError;
                }

                return scanResult;
            }
        }

        Logger.LogError("No handlers for the provided Argument Set were found.");
        return scanResult;
    }

    private async Task<ScanResult> SafelyExecuteAsync(BcdeExecutionTelemetryRecord record, Func<Task<ScanResult>> wrappedInvocation)
    {
        try
        {
            return await wrappedInvocation();
        }
        catch (Exception ae)
        {
            var result = new ScanResult()
            {
                ResultCode = ProcessingResultCode.Error,
            };

            var e = ae.GetBaseException();
            if (e is InvalidUserInputException)
            {
                Logger.LogError($"Something bad happened, is everything configured correctly?");
                Logger.LogException(e, isError: true, printException: true);

                record.ErrorMessage = e.ToString();
                result.ResultCode = ProcessingResultCode.InputError;

                return result;
            }
            else
            {
                // On an exception, return error to dotnet core
                Logger.LogError($"There was an unexpected error: ");
                Logger.LogException(e, isError: true);

                var errorMessage = new StringBuilder();
                errorMessage.AppendLine(e.ToString());
                if (e is ReflectionTypeLoadException refEx && refEx.LoaderExceptions != null)
                {
                    foreach (var loaderException in refEx.LoaderExceptions)
                    {
                        var loaderExceptionString = loaderException.ToString();
                        Logger.LogError(loaderExceptionString);
                        errorMessage.AppendLine(loaderExceptionString);
                    }
                }

                record.ErrorMessage = errorMessage.ToString();
                return result;
            }
        }
    }
}
