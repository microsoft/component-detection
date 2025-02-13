namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class Go117ComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private readonly HashSet<string> projectRoots = [];

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IGoParserFactory goParserFactory;

    public Go117ComponentDetector(
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
        this.Logger = logger;
        this.goParserFactory = new GoParserFactory(logger, fileUtilityService, commandLineInvocationService);
    }

    public Go117ComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IEnvironmentVariableService envVarService,
        ILogger<GoComponentDetector> logger,
        IFileUtilityService fileUtilityService,
        IGoParserFactory goParserFactory)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.Logger = logger;
        this.goParserFactory = goParserFactory;
    }

    public override string Id => "Go117";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod)];

    public override IList<string> SearchPatterns { get; } = ["go.mod", "go.sum"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Go];

    public override int Version => 8;

    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
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

                return GoDetectorUtils.ShouldRemoveGoSumFromDetection(goSumFilePath: processRequest.ComponentStream.Location, goModFile, this.Logger);
            }
            finally
            {
                goModFile?.Stream.Dispose();
            }
        });

        return Task.FromResult(goModProcessRequests);
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var projectRootDirectory = Directory.GetParent(file.Location);
        if (this.projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
        {
            return;
        }

        var record = new GoGraphTelemetryRecord();
        var fileExtension = Path.GetExtension(file.Location).ToUpperInvariant();
        switch (fileExtension)
        {
            case ".MOD":
            {
                this.Logger.LogDebug("Found Go.mod: {Location}", file.Location);
                await this.goParserFactory.CreateParser(GoParserType.GoMod).ParseAsync(singleFileComponentRecorder, file, record);

                if (await this.ShouldRunGoGraphAsync())
                {
                    await GoDependencyGraphUtility.GenerateAndPopulateDependencyGraphAsync(
                        this.commandLineInvocationService,
                        this.Logger,
                        singleFileComponentRecorder,
                        projectRootDirectory.FullName,
                        record,
                        cancellationToken);
                }

                break;
            }

            case ".SUM":
            {
                this.Logger.LogDebug("Found Go.sum: {Location}", file.Location);
                await this.goParserFactory.CreateParser(GoParserType.GoSum).ParseAsync(singleFileComponentRecorder, file, record);
                break;
            }

            default:
            {
                throw new InvalidOperationException("Unexpected file type detected in go detector");
            }
        }
    }

    private async Task<bool> ShouldRunGoGraphAsync()
    {
        var goVersion = await this.GetGoVersionAsync();
        if (goVersion == null)
        {
            return false;
        }

        return goVersion >= new Version(1, 11);
    }

    private async Task<Version> GetGoVersionAsync()
    {
        var processExecution = await this.commandLineInvocationService.ExecuteCommandAsync("go", null, null, cancellationToken: default, new List<string> { "version" }.ToArray());
        if (processExecution.ExitCode != 0)
        {
            return null;
        }

        // Define the regular expression pattern to match the version number
        var versionPattern = @"go version go(\d+\.\d+\.\d+)";
        var match = Regex.Match(processExecution.StdOut, versionPattern);

        if (match.Success)
        {
            // Extract the version number from the match
            var versionStr = match.Groups[1].Value;
            return new Version(versionStr);
        }

        return null;
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
}
