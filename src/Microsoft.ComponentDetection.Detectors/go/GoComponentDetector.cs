namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class GoComponentDetector : FileComponentDetector
{
    private readonly HashSet<string> projectRoots = [];

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly IGoParserFactory goParserFactory;

    public GoComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IEnvironmentVariableService envVarService,
        ILogger<GoComponentDetector> logger,
        IFileUtilityService fileUtilityService)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.envVarService = envVarService;
        this.Logger = logger;
        this.fileUtilityService = fileUtilityService;
        this.goParserFactory = new GoParserFactory(fileUtilityService, commandLineInvocationService);
    }

    public GoComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IEnvironmentVariableService envVarService,
        ILogger<GoComponentDetector> logger,
        IFileUtilityService fileUtilityService,
        IGoParserFactory factory)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.envVarService = envVarService;
        this.Logger = logger;
        this.fileUtilityService = fileUtilityService;
        this.goParserFactory = factory;
    }

    public override string Id => "Go";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod)];

    public override IList<string> SearchPatterns { get; } = ["go.mod", "go.sum"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Go];

    public override int Version => 8;

    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        // Filter out any go.sum process requests if the adjacent go.mod file is present and has a go version >= 1.17
        var goModProcessRequests = processRequests.Where(processRequest =>
        {
            if (Path.GetFileName(processRequest.ComponentStream.Location) != "go.sum")
            {
                return true;
            }

            var goModFile = this.FindAdjacentGoModComponentStreams(processRequest).FirstOrDefault();

            try
            {
                if (goModFile == null)
                {
                    this.Logger.LogDebug(
                        "go.sum file found without an adjacent go.mod file. Location: {Location}",
                        processRequest.ComponentStream.Location);

                    return true;
                }

                return GoDetectorUtils.ShouldIncludeGoSumFromDetection(goSumFilePath: processRequest.ComponentStream.Location, goModFile, this.Logger);
            }
            finally
            {
                goModFile?.Stream.Dispose();
            }
        });

        return Task.FromResult(goModProcessRequests);
    }

    private IEnumerable<ComponentStream> FindAdjacentGoModComponentStreams(ProcessRequest processRequest) =>
        this.ComponentStreamEnumerableFactory.GetComponentStreams(
                new FileInfo(processRequest.ComponentStream.Location).Directory,
                ["go.mod"],
                (_, _) => false,
                false)
            .Select(x =>
            {
                // The stream will be disposed at the end of this method, so we need to copy it to a new stream.
                var memoryStream = new MemoryStream();

                x.Stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return new ComponentStream
                {
                    Stream = memoryStream,
                    Location = x.Location,
                    Pattern = x.Pattern,
                };
            });

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var projectRootDirectory = Directory.GetParent(file.Location);
        if (this.projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
        {
            return;
        }

        using var record = new GoGraphTelemetryRecord();
        record.WasGoCliDisabled = false;
        record.WasGoFallbackStrategyUsed = false;

        var wasGoCliScanSuccessful = false;
        try
        {
            if (!this.IsGoCliManuallyDisabled())
            {
                wasGoCliScanSuccessful = await this.goParserFactory.CreateParser(GoParserType.GoCLI, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);
            }
            else
            {
                record.WasGoCliDisabled = true;
                this.Logger.LogInformation("Go cli scan was manually disabled, fallback strategy performed." +
                                           " More info: https://github.com/microsoft/component-detection/blob/main/docs/detectors/go.md#fallback-detection-strategy");
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to detect components using go cli. Location: {Location}", file.Location);
            record.ExceptionMessage = ex.Message;
        }
        finally
        {
            if (wasGoCliScanSuccessful)
            {
                this.projectRoots.Add(projectRootDirectory.FullName);
            }
            else
            {
                record.WasGoFallbackStrategyUsed = true;
                await this.ParseGoFileAsync(file, singleFileComponentRecorder, record);
            }
        }
    }

    private async Task ParseGoFileAsync(IComponentStream file, ISingleFileComponentRecorder singleFileComponentRecorder, GoGraphTelemetryRecord record)
    {
        var fileExtension = Path.GetExtension(file.Location).ToUpperInvariant();
        switch (fileExtension)
        {
            case ".MOD":
            {
                this.Logger.LogDebug("Found Go.mod: {Location}", file.Location);

                await this.goParserFactory.CreateParser(GoParserType.GoMod, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);
                break;
            }

            case ".SUM":
            {
                this.Logger.LogDebug("Found Go.sum: {Location}", file.Location);
                await this.goParserFactory.CreateParser(GoParserType.GoSum, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);
                break;
            }

            default:
            {
                throw new InvalidOperationException("Unexpected file type detected in go detector");
            }
        }
    }

    private bool IsGoCliManuallyDisabled()
    {
        return this.envVarService.IsEnvironmentVariableValueTrue("DisableGoCliScan");
    }
}
