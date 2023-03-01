namespace Microsoft.ComponentDetection.Orchestrator;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Extensions.Hosting;
using Serilog.Extensions.Logging;

public class Orchestrator
{
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private readonly IServiceProvider serviceProvider;
    private readonly IEnumerable<IArgumentHandlingService> argumentHandlers;
    private readonly IFileWritingService fileWritingService;
    private readonly IArgumentHelper argumentHelper;
    private readonly ILogger<Orchestrator> logger;

    public Orchestrator(
        IServiceProvider serviceProvider,
        IEnumerable<IArgumentHandlingService> argumentHandlers,
        IFileWritingService fileWritingService,
        IArgumentHelper argumentHelper,
        ILogger<Orchestrator> logger)
    {
        this.serviceProvider = serviceProvider;
        this.argumentHandlers = argumentHandlers;
        this.fileWritingService = fileWritingService;
        this.argumentHelper = argumentHelper;
        this.logger = logger;
    }

    public async Task<ScanResult> LoadAsync(string[] args, CancellationToken cancellationToken = default)
    {
        BaseArguments baseArguments = null;
        var parserResult = this.argumentHelper.ParseArguments<BaseArguments>(args, true);
        parserResult.WithParsed(x => baseArguments = x);
        if (parserResult.Tag == ParserResultType.NotParsed)
        {
            // Blank args for this part of the loader, all things are optional and default to false / empty / null
            baseArguments = new BaseArguments();
        }

        var logFile = Path.Combine(
            baseArguments.Output ?? Path.GetTempPath(),
            $"GovCompDisc_Log{DateTime.Now:yyyyMMddHHmmssfff}.log");

        var reloadableLogger = (ReloadableLogger)Log.Logger;
        reloadableLogger.Reload(configuration =>
            configuration
                .WriteTo.Console()
                .WriteTo.Async(x => x.File(logFile))
                .WriteTo.Providers(this.serviceProvider.GetRequiredService<LoggerProviderCollection>())
                .MinimumLevel.Is(baseArguments.LogLevel)
                .Enrich.FromLogContext());

        this.logger.LogInformation("Log file: {LogFile}", logFile);

        // This is required so TelemetryRelay can be accessed via it's static singleton
        // It should be refactored out at a later date
        TelemetryRelay.Instance.Init(this.serviceProvider.GetRequiredService<IEnumerable<ITelemetryService>>());
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
            this.logger.LogInformation("The scan had some detections complete while others encountered errors. The log file should indicate any issues that happened during the scan.");
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

    public async Task<ScanResult> HandleCommandAsync(
        string[] args,
        BcdeExecutionTelemetryRecord telemetryRecord,
        CancellationToken cancellationToken = default)
    {
        var scanResult = new ScanResult()
        {
            ResultCode = ProcessingResultCode.Error,
        };

        var parsedArguments = this.argumentHelper.ParseArguments(args);
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
                this.fileWritingService.Init(argumentSet.Output);

                this.logger.LogInformation("Run correlation id: {CorrelationId}", TelemetryConstants.CorrelationId);

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

        if (this.argumentHandlers == null)
        {
            this.logger.LogError("No argument handling services were registered.");
            return scanResult;
        }

        foreach (var handler in this.argumentHandlers)
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
                    this.logger.LogError(timeoutException, "The scan timed out.");
                    scanResult.ResultCode = ProcessingResultCode.TimeoutError;
                }

                return scanResult;
            }
        }

        this.logger.LogError("No handlers for the provided Argument Set were found.");
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
                this.logger.LogError(e, "Something bad happened, is everything configured correctly?");

                record.ErrorMessage = e.ToString();
                result.ResultCode = ProcessingResultCode.InputError;

                return result;
            }
            else
            {
                // On an exception, return error to dotnet core
                this.logger.LogError(e, "There was an unexpected error");

                var errorMessage = new StringBuilder();
                errorMessage.AppendLine(e.ToString());
                if (e is ReflectionTypeLoadException refEx && refEx.LoaderExceptions != null)
                {
                    foreach (var loaderException in refEx.LoaderExceptions)
                    {
                        this.logger.LogError(loaderException, "Got exception");
                        errorMessage.AppendLine(loaderException.ToString());
                    }
                }

                record.ErrorMessage = errorMessage.ToString();
                return result;
            }
        }
    }
}
