namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class PipReportComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private const string DisablePipReportScanEnvVar = "DisablePipReportScan";

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

    public PipReportComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPipCommandService pipCommandService,
        IEnvironmentVariableService envVarService,
        IPythonCommandService pythonCommandService,
        IPythonResolver pythonResolver,
        ILogger<PipReportComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.pipCommandService = pipCommandService;
        this.envVarService = envVarService;
        this.pythonCommandService = pythonCommandService;
        this.pythonResolver = pythonResolver;
        this.Logger = logger;
    }

    public override string Id => "PipReport";

    public override IList<string> SearchPatterns => new List<string> { "setup.py", "requirements.txt" };

    public override IEnumerable<string> Categories => new List<string> { "Python" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pip };

    public override int Version { get; } = 2;

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PipExePath", out var pipExePath);
        if (!await this.pipCommandService.PipExistsAsync(pipExePath))
        {
            this.Logger.LogInformation($"PipReport: No pip found on system. Pip installation report detection will not run.");

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        var pipVersion = await this.pipCommandService.GetPipVersionAsync(pipExePath);
        if (pipVersion is null || pipVersion < MinimumPipVersion)
        {
            this.Logger.LogInformation(
                "PipReport: No valid pip version found on system. {MinimumPipVersion} or greater is required. Pip installation report detection will not run.",
                MinimumPipVersion);

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PythonExePath", out var pythonExePath);
        if (!await this.pythonCommandService.PythonExistsAsync(pythonExePath))
        {
            this.Logger.LogInformation($"No python found on system. Python detection will not run.");

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }
        else
        {
            var pythonVersion = await this.pythonCommandService.GetPythonVersionAsync(pythonExePath);
            this.pythonResolver.SetPythonEnvironmentVariable("python_version", pythonVersion);

            var pythonPlatformString = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "win32"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? "linux"
                    : string.Empty;
            this.pythonResolver.SetPythonEnvironmentVariable("sys_platform", pythonPlatformString);
        }

        return processRequests;
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PipExePath", out var pipExePath);
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        FileInfo reportFile = null;
        try
        {
            if (this.IsPipReportManuallyDisabled())
            {
                this.Logger.LogWarning("PipReport: Found {DisablePipReportScanEnvVar} environment variable equal to true. Skipping pip report.", DisablePipReportScanEnvVar);
                using var skipReportRecord = new PipReportSkipTelemetryRecord
                {
                    SkipReason = $"PipReport: Found {DisablePipReportScanEnvVar} environment variable equal to true. Skipping pip report.",
                    DetectorId = this.Id,
                    DetectorVersion = this.Version,
                };

                return;
            }

            var stopwatch = Stopwatch.StartNew();
            this.Logger.LogInformation("PipReport: Generating pip installation report for {File}", file.Location);

            // Call pip executable to generate the installation report of a given project file.
            (var report, reportFile) = await this.pipCommandService.GenerateInstallationReportAsync(file.Location, pipExePath, cancellationToken);

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

            stopwatch.Stop();
            this.Logger.LogInformation(
                "PipReport: Generating pip installation report for {File} completed in {TotalSeconds} seconds with {PkgCount} detected packages.",
                file.Location,
                stopwatch.ElapsedMilliseconds / 1000.0,
                report.InstallItems?.Length ?? 0);

            // Now that all installed packages are known, we can build a graph of the dependencies.
            if (report.InstallItems is not null)
            {
                var graph = this.BuildGraphFromInstallationReport(report);
                this.RecordComponents(singleFileComponentRecorder, graph);
            }
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
        }
        finally
        {
            // Clean up the report output JSON file so it isn't left on the machine.
            if (reportFile is not null && reportFile.Exists)
            {
                reportFile.Delete();
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

        foreach (var package in report.InstallItems)
        {
            // Normalize the package name to ensure consistency between the package name and the graph nodes.
            var normalizedPkgName = PipReportUtilities.NormalizePackageNameFormat(package.Metadata.Name);

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
                var dependencySpec = new PipDependencySpecification($"Requires-Dist: {dependency}", requiresDist: true);
                if (!dependencySpec.IsValidParentPackage(this.pythonResolver.GetPythonEnvironmentVariables()))
                {
                    continue;
                }

                if (dependenciesByPkg.ContainsKey(normalizedPkgName))
                {
                    dependenciesByPkg[normalizedPkgName].Add(dependencySpec);
                }
                else
                {
                    dependenciesByPkg.Add(normalizedPkgName, new List<PipDependencySpecification> { dependencySpec });
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
            if (!node.Parents.Any())
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

    private bool IsPipReportManuallyDisabled()
        => this.envVarService.IsEnvironmentVariableValueTrue(DisablePipReportScanEnvVar);
}
