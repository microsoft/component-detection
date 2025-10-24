#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class PipReportComponentDetector : FileComponentDetectorWithCleanup
{
    // environment variables
    private const string PipReportOverrideBehaviorEnvVar = "PipReportOverrideBehavior";
    private const string PipReportSkipFallbackOnFailureEnvVar = "PipReportSkipFallbackOnFailure";
    private const string PipReportFileLevelTimeoutSecondsEnvVar = "PipReportFileLevelTimeoutSeconds";
    private const string PipReportPersistReportsEnvVar = "PipReportPersistReports";

    private static readonly IList<string> PipReportPreGeneratedFilePatterns = ["*.component-detection-pip-report.json", "component-detection-pip-report.json"];

    /// <summary>
    /// The maximum version of the report specification that this detector can handle.
    /// </summary>
    private static readonly Version MaxReportVersion = new(1, 0, 0);

    /// <summary>
    /// The minimum version of the pip utility that this detector can handle.
    /// </summary>
    private static readonly Version MinimumPipVersion = new(22, 2, 0);

    private readonly IPipCommandService pipCommandService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly IPythonCommandService pythonCommandService;
    private readonly IPythonResolver pythonResolver;
    private readonly IFileUtilityService fileUtilityService;

    public PipReportComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPipCommandService pipCommandService,
        IEnvironmentVariableService envVarService,
        IPythonCommandService pythonCommandService,
        IPythonResolver pythonResolver,
        IFileUtilityService fileUtilityService,
        IDirectoryUtilityService directoryUtilityService,
        ILogger<PipReportComponentDetector> logger)
        : base(fileUtilityService, directoryUtilityService)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.pipCommandService = pipCommandService;
        this.envVarService = envVarService;
        this.pythonCommandService = pythonCommandService;
        this.pythonResolver = pythonResolver;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    private enum PipReportOverrideBehavior
    {
        None, // do not override pip report
        Skip, // skip pip report altogether
        SourceCodeScan, // scan source code files, and record components explicitly from the package files without hitting a remote feed
    }

    public override string Id => "PipReport";

    public override IList<string> SearchPatterns => ["setup.py", "requirements.txt"];

    public override IEnumerable<string> Categories => ["Python"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Pip];

    public override int Version { get; } = 9;

    protected override bool EnableParallelism { get; set; } = true;

    protected override IList<string> CleanupPatterns => ["*.egg", "*.egg-info", "*.pyc", "*.pyo", "*.pyd", "__pycache__"];

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PipExePath", out var pipExePath);
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PythonExePath", out var pythonExePath);
        if (!await this.pipCommandService.PipExistsAsync(pipExePath, pythonExePath))
        {
            this.Logger.LogInformation($"PipReport: No pip found on system. Pip installation report detection will not run.");

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        var pipVersion = await this.pipCommandService.GetPipVersionAsync(pipExePath, pythonExePath);
        if (pipVersion is null || pipVersion < MinimumPipVersion)
        {
            this.Logger.LogInformation(
                "PipReport: No valid pip version found on system. {MinimumPipVersion} or greater is required. Pip installation report detection will not run.",
                MinimumPipVersion);

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        if (!await this.pythonCommandService.PythonExistsAsync(pythonExePath))
        {
            this.Logger.LogInformation($"No python found on system. Python detection will not run.");

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }
        else
        {
            var pythonVersion = await this.pythonCommandService.GetPythonVersionAsync(pythonExePath);
            this.pythonResolver.SetPythonEnvironmentVariable("python_version", pythonVersion);

            var pythonPlatformString = await this.pythonCommandService.GetOsTypeAsync(pythonExePath);
            this.pythonResolver.SetPythonEnvironmentVariable("sys_platform", pythonPlatformString);
        }

        return processRequests;
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PipExePath", out var pipExePath);
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PythonExePath", out var pythonExePath);
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        List<FileInfo> reportFiles = [];
        try
        {
            var pipOverride = this.GetPipReportOverrideBehavior();
            if (pipOverride == PipReportOverrideBehavior.SourceCodeScan)
            {
                this.Logger.LogInformation(
                    "PipReport: Found {PipReportOverrideBehaviorEnvVar} environment variable set to {Override}. Manually compiling" +
                    " dependency list for '{File}' without reaching out to a remote feed.",
                    PipReportOverrideBehaviorEnvVar,
                    PipReportOverrideBehavior.SourceCodeScan.ToString(),
                    file.Location);

                await this.RegisterExplicitComponentsInFileAsync(singleFileComponentRecorder, file.Location, pythonExePath);
                return;
            }
            else if (pipOverride == PipReportOverrideBehavior.Skip)
            {
                var skipReason = $"PipReport: Found {PipReportOverrideBehaviorEnvVar} environment variable set " +
                    $"to {PipReportOverrideBehavior.Skip}. Skipping pip detection for '{file.Location}'.";

                this.Logger.LogInformation("{Message}", skipReason);
                using var skipReportRecord = new PipReportSkipTelemetryRecord
                {
                    SkipReason = skipReason,
                    DetectorId = this.Id,
                    DetectorVersion = this.Version,
                };

                return;
            }

            var stopwatch = Stopwatch.StartNew();
            using var pipReportTypeRecord = new PipReportTypeTelemetryRecord
            {
                FilePath = file.Location,
            };

            // Search for a pre-generated pip report file in the same directory as the file being scanned.
            var fileParentDirectory = Path.GetDirectoryName(file.Location);
            if (fileParentDirectory is null)
            {
                this.Logger.LogWarning("PipReport: Unable to determine parent directory for {File}.", file.Location);
                return;
            }

            var fileParentDirectoryInfo = Directory.Exists(fileParentDirectory)
                ? new DirectoryInfo(fileParentDirectory)
                : null;

            List<FileInfo> preGeneratedReportFiles = null;
            if (fileParentDirectoryInfo is not null)
            {
                preGeneratedReportFiles = PipReportPreGeneratedFilePatterns
                    .SelectMany(pattern => fileParentDirectoryInfo.GetFiles(pattern))
                    .Where(file => File.Exists(file.FullName))
                    .ToList();
            }

            List<PipInstallationReport> reports = [];
            if (preGeneratedReportFiles is not null && preGeneratedReportFiles.Count != 0)
            {
                this.Logger.LogInformation("PipReport: Found pre-generated pip report(s) for {File}.", file.Location);

                foreach (var existingReport in preGeneratedReportFiles)
                {
                    this.Logger.LogInformation("PipReport: Using pre-generated pip report '{ReportFile}' for package file '{File}'.", existingReport.FullName, file.Location);
                    var reportOutput = await this.fileUtilityService.ReadAllTextAsync(existingReport);
                    var report = JsonConvert.DeserializeObject<PipInstallationReport>(reportOutput);

                    if (await this.IsValidPreGeneratedReportAsync(report, pythonExePath, file.Location))
                    {
                        reports.Add(report);
                    }
                    else
                    {
                        this.Logger.LogInformation(
                            "PipReport: Pre-generated pip report '{ReportFile}' is invalid. Did not contain all requested components in package file '{File}'.",
                            existingReport.FullName,
                            file.Location);
                    }
                }
            }

            var foundPreGeneratedReport = reports.Count != 0;
            pipReportTypeRecord.PreGenerated = foundPreGeneratedReport;
            if (!foundPreGeneratedReport)
            {
                this.Logger.LogInformation("PipReport: Generating pip installation report for {File}", file.Location);
                pipReportTypeRecord.PreGenerated = false;

                // create linked cancellation token that will cancel if the file level timeout is reached, or if the parent token is cancelled.
                // default to only using parent token if the env var is not set or is invalid
                var childCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (this.envVarService.DoesEnvironmentVariableExist(PipReportFileLevelTimeoutSecondsEnvVar)
                    && int.TryParse(this.envVarService.GetEnvironmentVariable(PipReportFileLevelTimeoutSecondsEnvVar), out var timeoutSeconds))
                {
                    childCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                }

                // Call pip executable to generate the installation report of a given project file.
                (var report, var reportFile) = await this.pipCommandService.GenerateInstallationReportAsync(file.Location, pipExePath, pythonExePath, childCts.Token);
                reports.Add(report);
                reportFiles.Add(reportFile);
            }

            if (reports.Count == 0)
            {
                this.Logger.LogWarning("PipReport: Failed to generate or find pip installation report for {File}.", file.Location);
                return;
            }

            foreach (var report in reports)
            {
                // The report version is used to determine how to parse the report. If it is greater
                // than the maximum supported version, there may be new fields and the parsing will fail.
                if (!int.TryParse(report.Version, out var reportVersion) || reportVersion > MaxReportVersion.Major)
                {
                    this.Logger.LogWarning(
                        "PipReport: The pip installation report version {ReportVersion} is not supported. The maximum supported version is {MaxVersion}.",
                        report.Version,
                        MaxReportVersion);

                    using var versionRecord = new InvalidParseVersionTelemetryRecord
                    {
                        DetectorId = this.Id,
                        FilePath = file.Location,
                        Version = report.Version,
                        MaxVersion = MaxReportVersion.ToString(),
                    };

                    return;
                }

                // Now that all installed packages are known, we can build a graph of the dependencies.
                if (report.InstallItems is not null)
                {
                    var graph = this.BuildGraphFromInstallationReport(report);
                    this.RecordComponents(singleFileComponentRecorder, graph);
                }
            }

            var packageCount = singleFileComponentRecorder.GetDetectedComponents()?.Keys?.ToImmutableHashSet().Count ?? 0;
            pipReportTypeRecord.PackageCount = packageCount;
            this.Logger.LogInformation(
                "PipReport: Pip installation report for {File} completed in {TotalSeconds} seconds with {PkgCount} detected packages.",
                file.Location,
                stopwatch.ElapsedMilliseconds / 1000.0,
                packageCount);

            stopwatch.Stop();
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "PipReport: Failure while parsing pip installation report for {File}", file.Location);

            using var parseFailedRecord = new FailedParsingFileRecord
            {
                DetectorId = this.Id,
                FilePath = file.Location,
                ExceptionMessage = e.Message,
                StackTrace = e.StackTrace,
            };

            // if pipreport fails, try to at least list the dependencies that are found in the source files
            if (this.GetPipReportOverrideBehavior() != PipReportOverrideBehavior.SourceCodeScan && !this.PipReportSkipFallbackOnFailure())
            {
                try
                {
                    this.Logger.LogInformation(
                        "PipReport: Trying to manually compile package list for '{File}' without reaching out to a remote feed. " +
                        "This will NOT create a dependency graph, so should be avoided unless absolutely necessary.",
                        file.Location);
                    await this.RegisterExplicitComponentsInFileAsync(singleFileComponentRecorder, file.Location, pythonExePath);
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "PipReport: Failed to manually compile package list for '{File}'.", file.Location);
                }
            }
        }
        finally
        {
            // Clean up the report output JSON file so it isn't left on the machine.
            if (!this.envVarService.IsEnvironmentVariableValueTrue(PipReportPersistReportsEnvVar))
            {
                foreach (var reportFile in reportFiles)
                {
                    if (reportFile is not null && reportFile.Exists)
                    {
                        reportFile.Delete();
                    }
                }
            }
        }
    }

    private Dictionary<string, PipReportGraphNode> BuildGraphFromInstallationReport(PipInstallationReport report)
    {
        // The installation report contains a list of all installed packages, including their dependencies.
        // However, dependencies do not have explicitly marked root packages so we will need to build the
        // graph ourselves using the requires_dist field.
        var dependenciesByPkg = new Dictionary<string, List<PipDependencySpecification>>(StringComparer.OrdinalIgnoreCase);
        var nodeReferences = new Dictionary<string, PipReportGraphNode>(StringComparer.OrdinalIgnoreCase);
        var pythonEnvVars = this.pythonResolver.GetPythonEnvironmentVariables();

        foreach (var package in report.InstallItems)
        {
            // Normalize the package name to ensure consistency between the package name and the graph nodes.
            var normalizedPkgName = PipReportUtilities.NormalizePackageNameFormat(package.Metadata.Name);
            if (PipDependencySpecification.PackagesToIgnore.Contains(normalizedPkgName))
            {
                continue;
            }

            if (!PipReportUtilities.IsCanonicalVersion(package.Metadata.Version))
            {
                this.Logger.LogWarning(
                    "PipReport: Skipping package '{Package}' with non-canonical version '{Version}'. " +
                    "See https://www.python.org/dev/peps/pep-0440/#appendix-b-parsing-version-strings-with-regular-expressions",
                    normalizedPkgName,
                    package.Metadata.Version);
                continue;
            }

            var node = new PipReportGraphNode(
                new PipComponent(
                    normalizedPkgName,
                    package.Metadata.Version,
                    author: PipReportUtilities.GetSupplierFromInstalledItem(package),
                    license: PipReportUtilities.GetLicenseFromInstalledItem(package)),
                package.Requested);

            nodeReferences.Add(normalizedPkgName, node);

            // requires_dist will contain information about the dependencies of the package.
            // However, we don't have PipReportGraphNodes for all dependencies, so we will use
            // an intermediate layer to store the relationships and update the graph later.
            if (package.Metadata?.RequiresDist is null)
            {
                continue;
            }

            foreach (var dependency in package.Metadata.RequiresDist)
            {
                // Dependency strings can be in the form of:
                // cffi (>=1.12)
                // futures; python_version <= \"2.7\"
                // sphinx (!=1.8.0,!=3.1.0,!=3.1.1,>=1.6.5) ; extra == 'docs'
                var dependencySpec = new PipDependencySpecification(this.Logger, $"Requires-Dist: {dependency}", requiresDist: true);
                if (!dependencySpec.IsValidParentPackage(pythonEnvVars))
                {
                    continue;
                }

                if (dependenciesByPkg.ContainsKey(normalizedPkgName))
                {
                    dependenciesByPkg[normalizedPkgName].Add(dependencySpec);
                }
                else
                {
                    dependenciesByPkg.Add(normalizedPkgName, [dependencySpec]);
                }
            }
        }

        // Update the graph with their dependency relationships.
        foreach (var dependency in dependenciesByPkg)
        {
            var rootNode = nodeReferences[dependency.Key];

            // Update the "root" dependency.
            foreach (var child in dependency.Value)
            {
                var normalizedChildName = PipReportUtilities.NormalizePackageNameFormat(child.Name);

                if (!nodeReferences.ContainsKey(normalizedChildName))
                {
                    // This dependency is not in the report, so we can't add it to the graph.
                    // Known potential causes: python_version/sys_platform specification.
                    continue;
                }

                var childNode = nodeReferences[normalizedChildName];
                rootNode.Children.Add(childNode);

                // Add the link to the parent dependency.
                childNode.Parents.Add(rootNode);
            }
        }

        return nodeReferences;
    }

    private void RecordComponents(
        ISingleFileComponentRecorder recorder,
        Dictionary<string, PipReportGraphNode> graph)
    {
        // Explicit root packages are marked with a requested flag.
        // Parent components must be registered before their children.
        foreach (var node in graph.Values)
        {
            var component = new DetectedComponent(node.Value);
            recorder.RegisterUsage(
                component,
                isExplicitReferencedDependency: node.Requested);
        }

        // Once the graph has been populated with all dependencies, we can register the relationships.
        // Ideally this would happen in the same loop as the previous one, but we need to ensure that
        // parentComponentId is guaranteed to exist in the graph or an exception will be thrown.
        foreach (var node in graph.Values)
        {
            if (node.Parents.Count == 0)
            {
                continue;
            }

            var component = new DetectedComponent(node.Value);
            foreach (var parent in node.Parents)
            {
                recorder.RegisterUsage(
                    component,
                    isExplicitReferencedDependency: node.Requested,
                    parentComponentId: parent.Value.Id);
            }
        }
    }

    private async Task RegisterExplicitComponentsInFileAsync(
        ISingleFileComponentRecorder recorder,
        string filePath,
        string pythonPath = null)
    {
        var initialPackages = await this.pythonCommandService.ParseFileAsync(filePath, pythonPath);
        if (initialPackages == null)
        {
            return;
        }

        var listedPackage = SharedPipUtilities.ParsedPackagesToPipDependencies(
                initialPackages,
                this.pythonResolver.GetPythonEnvironmentVariables(),
                this.Logger)
            .ToList();

        listedPackage.Select(x => (x.Name, Version: x.GetHighestExplicitPackageVersion()))
            .Where(x => !string.IsNullOrEmpty(x.Version))
            .Select(x => new PipComponent(x.Name, x.Version))
            .Select(x => new DetectedComponent(x))
            .ToList()
            .ForEach(pipComponent => recorder.RegisterUsage(pipComponent, isExplicitReferencedDependency: true));

        initialPackages.Where(tuple => tuple.Component != null)
            .Select(tuple => new DetectedComponent(tuple.Component))
            .ToList()
            .ForEach(gitComponent => recorder.RegisterUsage(gitComponent, isExplicitReferencedDependency: true));
    }

    /// <summary>
    /// Confirms that the detected report at least contains all of the packages directly requested
    /// in the pip file. This prevents invalid reports from being used to create the dependency graph.
    /// </summary>
    private async Task<bool> IsValidPreGeneratedReportAsync(PipInstallationReport report, string pythonExePath, string filePath)
    {
        try
        {
            var initialPackages = await this.pythonCommandService.ParseFileAsync(filePath, pythonExePath);
            var listedPackage = SharedPipUtilities.ParsedPackagesToPipDependencies(
                    initialPackages,
                    this.pythonResolver.GetPythonEnvironmentVariables(),
                    this.Logger)
                .Select(x => x.Name)
                .ToImmutableSortedSet();

            var reportRequestedPackages = report.InstallItems
                .Where(package => package.Requested)
                .Select(package => package.Metadata.Name)
                .ToImmutableSortedSet();

            return listedPackage.IsSubsetOf(reportRequestedPackages);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "PipReport: Failed to validate pre-generated report for {File}", filePath);
            return false;
        }
    }

    private PipReportOverrideBehavior GetPipReportOverrideBehavior()
    {
        if (!this.envVarService.DoesEnvironmentVariableExist(PipReportOverrideBehaviorEnvVar))
        {
            return PipReportOverrideBehavior.None;
        }

        if (string.Equals(this.envVarService.GetEnvironmentVariable(PipReportOverrideBehaviorEnvVar), PipReportOverrideBehavior.SourceCodeScan.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return PipReportOverrideBehavior.SourceCodeScan;
        }
        else if (string.Equals(this.envVarService.GetEnvironmentVariable(PipReportOverrideBehaviorEnvVar), PipReportOverrideBehavior.Skip.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return PipReportOverrideBehavior.Skip;
        }

        return PipReportOverrideBehavior.None;
    }

    private bool PipReportSkipFallbackOnFailure()
    {
        return this.envVarService.IsEnvironmentVariableValueTrue(PipReportSkipFallbackOnFailureEnvVar);
    }
}
