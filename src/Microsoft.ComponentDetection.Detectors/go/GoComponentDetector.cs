#nullable disable
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

public class GoComponentDetector : FileComponentDetector
{
    private readonly HashSet<string> projectRoots = [];

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IGoParserFactory goParserFactory;
    private readonly IEnvironmentVariableService envVarService;

    public GoComponentDetector(
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
        this.envVarService = envVarService;
    }

    public override string Id => "Go";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod)];

    public override IList<string> SearchPatterns { get; } = ["go.mod", "go.sum"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Go];

    public override int Version => 10;

    protected async override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        var filteredGoProcessRequests = await processRequests.Where(processRequest =>
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
        }).ToList(); // Materialize the filtered items for sorting

        // Sort by depth: shallow files (fewer directory segments) come first
        var sortedGoProcessRequests = filteredGoProcessRequests
            .OrderBy(pr => pr.ComponentStream.Location.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(pr => pr.ComponentStream.Location)
            .ToList();

        return sortedGoProcessRequests.ToObservable();
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

        using var record = new GoGraphTelemetryRecord();
        var wasGoCliDisabled = this.IsGoCliManuallyDisabled();
        record.WasGoCliDisabled = wasGoCliDisabled;
        record.WasGoFallbackStrategyUsed = false;

        var fileExtension = Path.GetExtension(file.Location).ToUpperInvariant();
        switch (fileExtension)
        {
            case ".MOD":
            {
                this.Logger.LogDebug("Found Go.mod: {Location}", file.Location);
                var wasModParsedSuccessfully = await this.goParserFactory.CreateParser(GoParserType.GoMod, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);

                // Check if go.mod was parsed successfully and Go version is >= 1.17 in go.mod
                if (wasModParsedSuccessfully &&
                    !string.IsNullOrEmpty(record.GoModVersion) &&
                    System.Version.TryParse(record.GoModVersion, out var goVersion) &&
                    goVersion >= new Version(1, 17))
                {
                    this.projectRoots.Add(projectRootDirectory.FullName);
                }
                else
                {
                    this.Logger.LogDebug("Not adding {Root} to processed roots: {ParseSuccess} {GoModVersion}", projectRootDirectory.FullName, wasModParsedSuccessfully, record.GoModVersion);
                }

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

                // check if we can use Go CLI instead
                var wasGoCliScanSuccessful = false;

                try
                {
                    if (!wasGoCliDisabled)
                    {
                        wasGoCliScanSuccessful = await this.goParserFactory.CreateParser(GoParserType.GoCLI, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);
                    }
                    else
                    {
                        this.Logger.LogInformation("Go cli scan was manually disabled, fallback strategy performed.");
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Failed to detect components using go cli. Location: {Location}", file.Location);
                    record.ExceptionMessage = ex.Message;
                }

                this.Logger.LogDebug("Status of Go CLI scan when considering {GoSumLocation}: {Status}", file.Location, wasGoCliScanSuccessful);

                // If Go CLI scan was not successful/disabled, scan go.sum because this go.sum was recorded due to go.mod
                // containing go < 1.17. So go.mod is incomplete. We need to parse go.sum to make list of dependencies complete
                if (!wasGoCliScanSuccessful)
                {
                    record.WasGoFallbackStrategyUsed = true;
                    this.Logger.LogDebug("Go CLI scan when considering {GoSumLocation} was not successful. Falling back to scanning go.sum", file.Location);
                    await this.goParserFactory.CreateParser(GoParserType.GoSum, this.Logger).ParseAsync(singleFileComponentRecorder, file, record);
                }
                else
                {
                    this.projectRoots.Add(projectRootDirectory.FullName);
                }

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

    private async Task<bool> ShouldRunGoGraphAsync()
    {
        if (this.IsGoCliManuallyDisabled())
        {
            return false;
        }

        var goVersion = await this.GetGoVersionAsync();
        if (goVersion == null)
        {
            return false;
        }

        return goVersion >= new Version(1, 11);
    }

    private async Task<Version> GetGoVersionAsync()
    {
        try
        {
            var isGoAvailable = await this.commandLineInvocationService.CanCommandBeLocatedAsync("go", null, null, new List<string> { "version" }.ToArray());
            if (!isGoAvailable)
            {
                this.Logger.LogInformation("Go CLI was not found in the system");
                return null;
            }

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
        }
        catch (Exception e)
        {
            this.Logger.LogWarning("Failed to get go version: {Exception}", e);
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
