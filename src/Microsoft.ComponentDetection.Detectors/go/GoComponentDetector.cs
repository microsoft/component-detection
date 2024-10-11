namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Newtonsoft.Json;

public class GoComponentDetector : FileComponentDetector
{
    private const string StartString = "require ";

    private static readonly Regex GoSumRegex = new(
        @"(?<name>.*)\s+(?<version>.*?)(/go\.mod)?\s+(?<hash>.*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

    private readonly HashSet<string> projectRoots = [];

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly IFileUtilityService fileUtilityService;

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

            if (goModFile == null)
            {
                this.Logger.LogDebug(
                    "go.sum file found without an adjacent go.mod file. Location: {Location}",
                    processRequest.ComponentStream.Location);

                return true;
            }

            // parse the go.mod file to get the go version
            using var reader = new StreamReader(goModFile.Stream);
            var goModFileContents = reader.ReadToEnd();
            goModFile.Stream.Dispose();

            return this.CheckGoModVersion(goModFileContents, processRequest, goModFile);
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

    private bool CheckGoModVersion(string goModFileContents, ProcessRequest processRequest, ComponentStream goModFile)
    {
        var goVersionMatch = Regex.Match(goModFileContents, @"go\s(?<version>\d+\.\d+)");

        if (!goVersionMatch.Success)
        {
            this.Logger.LogDebug(
                "go.sum file found with an adjacent go.mod file that does not contain a go version. Location: {Location}",
                processRequest.ComponentStream.Location);
            return true;
        }

        var goVersion = goVersionMatch.Groups["version"].Value;
        if (System.Version.TryParse(goVersion, out var version))
        {
            if (version < new Version(1, 17))
            {
                this.Logger.LogWarning(
                    "go.mod file at {GoModLocation} does not have a go version >= 1.17. Scanning this go.sum file: {GoSumLocation} which may lead to over reporting components",
                    goModFile.Location,
                    processRequest.ComponentStream.Location);

                return true;
            }

            this.Logger.LogInformation(
                "go.sum file found with an adjacent go.mod file that has a go version >= 1.17. Will not scan this go.sum file. Location: {Location}",
                processRequest.ComponentStream.Location);

            return false;
        }

        this.Logger.LogWarning(
            "go.sum file found with an adjacent go.mod file that has an invalid go version. Scanning both for components. Location: {Location}",
            processRequest.ComponentStream.Location);

        return true;
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
        record.WasGoCliDisabled = false;
        record.WasGoFallbackStrategyUsed = false;

        var wasGoCliScanSuccessful = false;
        try
        {
            if (!this.IsGoCliManuallyDisabled())
            {
                wasGoCliScanSuccessful = await this.UseGoCliToScanAsync(file.Location, singleFileComponentRecorder, record);
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
                var fileExtension = Path.GetExtension(file.Location).ToUpperInvariant();
                switch (fileExtension)
                {
                    case ".MOD":
                    {
                        this.Logger.LogDebug("Found Go.mod: {Location}", file.Location);
                        await this.ParseGoModFileAsync(singleFileComponentRecorder, file, record);
                        break;
                    }

                    case ".SUM":
                    {
                        this.Logger.LogDebug("Found Go.sum: {Location}", file.Location);
                        this.ParseGoSumFile(singleFileComponentRecorder, file);
                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException("Unexpected file type detected in go detector");
                    }
                }
            }
        }
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "False positive")]
    private async Task<bool> UseGoCliToScanAsync(string location, ISingleFileComponentRecorder singleFileComponentRecorder, GoGraphTelemetryRecord record)
    {
        record.WasGraphSuccessful = false;
        record.DidGoCliCommandFail = false;
        var projectRootDirectory = Directory.GetParent(location);
        record.ProjectRoot = projectRootDirectory.FullName;

        var isGoAvailable = await this.commandLineInvocationService.CanCommandBeLocatedAsync("go", null, workingDirectory: projectRootDirectory, ["version"]);
        record.IsGoAvailable = isGoAvailable;

        if (!isGoAvailable)
        {
            this.Logger.LogInformation("Go CLI was not found in the system");
            return false;
        }

        this.Logger.LogInformation("Go CLI was found in system and will be used to generate dependency graph. " +
                                   "Detection time may be improved by activating fallback strategy (https://github.com/microsoft/component-detection/blob/main/docs/detectors/go.md#fallback-detection-strategy). " +
                                   "But, it will introduce noise into the detected components.");
        var goDependenciesProcess = await this.commandLineInvocationService.ExecuteCommandAsync("go", null, workingDirectory: projectRootDirectory, ["list", "-mod=readonly", "-m", "-json", "all"]);
        if (goDependenciesProcess.ExitCode != 0)
        {
            this.Logger.LogError("Go CLI command \"go list -m -json all\" failed with error: {GoDependenciesProcessStdErr}", goDependenciesProcess.StdErr);
            this.Logger.LogError("Go CLI could not get dependency build list at location: {Location}. Fallback go.sum/go.mod parsing will be used.", location);
            record.DidGoCliCommandFail = true;
            record.GoCliCommandError = goDependenciesProcess.StdErr;
            return false;
        }

        this.RecordBuildDependencies(goDependenciesProcess.StdOut, singleFileComponentRecorder, projectRootDirectory.FullName);

        var generateGraphProcess = await this.commandLineInvocationService.ExecuteCommandAsync("go", null, workingDirectory: projectRootDirectory, new List<string> { "mod", "graph" }.ToArray());
        if (generateGraphProcess.ExitCode == 0)
        {
            this.PopulateDependencyGraph(generateGraphProcess.StdOut, singleFileComponentRecorder);
            record.WasGraphSuccessful = true;
        }

        return true;
    }

    private void TryRegisterDependencyFromModLine(string line, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        if (line.Trim().StartsWith("//"))
        {
            // this is a comment line, ignore it
            return;
        }

        if (this.TryToCreateGoComponentFromModLine(line, out var goComponent))
        {
            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
        }
        else
        {
            var lineTrim = line.Trim();
            this.Logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
            singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
        }
    }

    private async Task ParseGoModFileAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord goGraphTelemetryRecord)
    {
        using var reader = new StreamReader(file.Stream);

        // There can be multiple require( ) sections in go 1.17+. loop over all of them.
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            while (line != null && !line.StartsWith("require ("))
            {
                if (line.StartsWith("go "))
                {
                    goGraphTelemetryRecord.GoModVersion = line[3..].Trim();
                }

                // In go >= 1.17, direct dependencies are listed as "require x/y v1.2.3", and transitive dependencies
                // are listed in the require () section
                if (line.StartsWith(StartString))
                {
                    this.TryRegisterDependencyFromModLine(line[StartString.Length..], singleFileComponentRecorder);
                }

                line = await reader.ReadLineAsync();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = await reader.ReadLineAsync()) != null && !line.EndsWith(')'))
            {
                this.TryRegisterDependencyFromModLine(line, singleFileComponentRecorder);
            }
        }
    }

    private bool TryToCreateGoComponentFromModLine(string line, out GoComponent goComponent)
    {
        var lineComponents = Regex.Split(line.Trim(), @"\s+");

        if (lineComponents.Length < 2)
        {
            goComponent = null;
            return false;
        }

        var name = lineComponents[0];
        var version = lineComponents[1];
        goComponent = new GoComponent(name, version);

        return true;
    }

    // For more information about the format of the go.sum file
    // visit https://golang.org/cmd/go/#hdr-Module_authentication_using_go_sum
    private void ParseGoSumFile(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file)
    {
        using var reader = new StreamReader(file.Stream);

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (this.TryToCreateGoComponentFromSumLine(line, out var goComponent))
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
            }
            else
            {
                var lineTrim = line.Trim();
                this.Logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
                singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
            }
        }
    }

    private bool TryToCreateGoComponentFromSumLine(string line, out GoComponent goComponent)
    {
        var m = GoSumRegex.Match(line);
        if (m.Success)
        {
            goComponent = new GoComponent(m.Groups["name"].Value, m.Groups["version"].Value, m.Groups["hash"].Value);
            return true;
        }

        goComponent = null;
        return false;
    }

    /// <summary>
    /// This command only adds edges between parent and child components, it does not add nor remove any entries from the existing build list.
    /// </summary>
    private void PopulateDependencyGraph(string goGraphOutput, ISingleFileComponentRecorder componentRecorder)
    {
        // Yes, go always returns \n even on Windows
        var graphRelationships = goGraphOutput.Split('\n');

        foreach (var relationship in graphRelationships)
        {
            var components = relationship.Split(' ');
            if (components.Length != 2)
            {
                if (string.IsNullOrWhiteSpace(relationship))
                {
                    // normally the last line is an empty string
                    continue;
                }

                this.Logger.LogWarning("Unexpected relationship output from go mod graph: {Relationship}", relationship);
                continue;
            }

            var isParentParsed = this.TryCreateGoComponentFromRelationshipPart(components[0], out var parentComponent);
            var isChildParsed = this.TryCreateGoComponentFromRelationshipPart(components[1], out var childComponent);

            if (!isParentParsed)
            {
                // These are explicit dependencies, we already have those recorded
                continue;
            }

            if (isChildParsed)
            {
                if (this.IsModuleInBuildList(componentRecorder, parentComponent) && this.IsModuleInBuildList(componentRecorder, childComponent))
                {
                    componentRecorder.RegisterUsage(new DetectedComponent(childComponent), parentComponentId: parentComponent.Id);
                }
            }
            else
            {
                this.Logger.LogWarning("Failed to parse components from relationship string {Relationship}", relationship);
                componentRecorder.RegisterPackageParseFailure(relationship);
            }
        }
    }

    private bool IsModuleInBuildList(ISingleFileComponentRecorder singleFileComponentRecorder, GoComponent component)
    {
        return singleFileComponentRecorder.GetComponent(component.Id) != null;
    }

    private void RecordBuildDependencies(string goListOutput, ISingleFileComponentRecorder singleFileComponentRecorder, string projectRootDirectoryFullName)
    {
        var goBuildModules = new List<GoBuildModule>();
        var reader = new JsonTextReader(new StringReader(goListOutput))
        {
            SupportMultipleContent = true,
        };

        using var record = new GoReplaceTelemetryRecord();

        while (reader.Read())
        {
            var serializer = new JsonSerializer();
            var buildModule = serializer.Deserialize<GoBuildModule>(reader);

            goBuildModules.Add(buildModule);
        }

        foreach (var dependency in goBuildModules)
        {
            var dependencyName = $"{dependency.Path} {dependency.Version}";

            if (dependency.Main)
            {
                // main is the entry point module (superfluous as we already have the file location)
                continue;
            }

            if (dependency.Replace?.Path != null && dependency.Replace.Version == null)
            {
                var dirName = projectRootDirectoryFullName;
                var combinedPath = Path.Combine(dirName, dependency.Replace.Path, "go.mod");
                var goModFilePath = Path.GetFullPath(combinedPath);
                if (this.fileUtilityService.Exists(goModFilePath))
                {
                    this.Logger.LogInformation("go Module {GoModule} is being replaced with module at path {GoModFilePath}", dependencyName, goModFilePath);
                    record.GoModPathAndVersion = dependencyName;
                    record.GoModReplacement = goModFilePath;
                    continue;
                }

                this.Logger.LogWarning("go.mod file {GoModFilePath} does not exist in the relative path given for replacement", goModFilePath);
                record.GoModPathAndVersion = goModFilePath;
                record.GoModReplacement = null;
            }

            GoComponent goComponent;
            if (dependency.Replace?.Path != null && dependency.Replace.Version != null)
            {
                try
                {
                    var dependencyReplacementName = $"{dependency.Replace.Path} {dependency.Replace.Version}";
                    goComponent = new GoComponent(dependency.Replace.Path, dependency.Replace.Version);
                    this.Logger.LogInformation("go Module {GoModule} being replaced with module {GoModuleReplacement}", dependencyName, dependencyReplacementName);
                    record.GoModPathAndVersion = dependencyName;
                    record.GoModReplacement = dependencyReplacementName;
                }
                catch (Exception ex)
                {
                    record.ExceptionMessage = ex.Message;
                    goComponent = new GoComponent(dependency.Path, dependency.Version);
                }
            }
            else
            {
                goComponent = new GoComponent(dependency.Path, dependency.Version);
            }

            if (dependency.Indirect)
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
            }
            else
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent), isExplicitReferencedDependency: true);
            }
        }
    }

    private bool TryCreateGoComponentFromRelationshipPart(string relationship, out GoComponent goComponent)
    {
        var componentParts = relationship.Split('@');
        if (componentParts.Length != 2)
        {
            goComponent = null;
            return false;
        }

        goComponent = new GoComponent(componentParts[0], componentParts[1]);
        return true;
    }

    private bool IsGoCliManuallyDisabled()
    {
        return this.envVarService.IsEnvironmentVariableValueTrue("DisableGoCliScan");
    }

    private class GoBuildModule
    {
        public string Path { get; set; }

        public bool Main { get; set; }

        public string Version { get; set; }

        public bool Indirect { get; set; }

        public GoBuildModule Replace { get; set; }
    }
}
