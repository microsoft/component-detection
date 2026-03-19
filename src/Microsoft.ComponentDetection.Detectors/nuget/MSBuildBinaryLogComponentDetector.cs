namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.Frameworks;
using global::NuGet.ProjectModel;
using Microsoft.Build.Framework;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.DotNet;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

/// <summary>
/// An experimental detector that combines MSBuild binlog information with NuGet project.assets.json
/// to provide enhanced component detection with project-level classifications.
/// This detector is intended to replace both DotNetComponentDetector and NuGetProjectModelProjectCentricComponentDetector.
/// </summary>
/// <remarks>
/// <para>
/// Logic consistency notes with replaced detectors:
/// </para>
/// <para>
/// <b>NuGet component detection (from NuGetProjectModelProjectCentricComponentDetector):</b>
/// - Uses the same LockFileUtilities methods for processing project.assets.json
/// - Maintains the same logic for determining framework packages and development dependencies
/// - Registers PackageDownload dependencies the same way
/// - Uses project path from RestoreMetadata.ProjectPath for component recorder (consistent behavior).
/// </para>
/// <para>
/// <b>DotNet component detection (from DotNetComponentDetector):</b>
/// - SDK version: Binlog provides NETCoreSdkVersion which is the actual version used during build
///   (more accurate than running `dotnet --version` which may differ due to global.json rollforward)
/// - Target type: Uses OutputType property from binlog to determine "application" vs "library"
///   (DotNetComponentDetector uses PE header inspection which requires compiled output to exist.)
/// - Target frameworks: Uses TargetFramework/TargetFrameworks properties from binlog.
///   (DotNetComponentDetector uses targets from project.assets.json which is equivalent.)
/// </para>
/// <para>
/// <b>Additional enhancements:</b>
/// - IsTestProject classification: All dependencies of test projects are marked as dev dependencies.
/// - Fallback mode: When no binlog info is available, falls back to standard NuGet detection.
/// </para>
/// </remarks>
public class MSBuildBinaryLogComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private readonly IBinLogProcessor binLogProcessor;
    private readonly IFileUtilityService fileUtilityService;
    private readonly DotNetProjectInfoProvider projectInfoProvider;
    private readonly LockFileFormat lockFileFormat = new();

    /// <summary>
    /// Stores project information extracted from binlogs, keyed by assets file path.
    /// </summary>
    /// <remarks>
    /// All binlog files are processed eagerly in <see cref="OnPrepareDetectionAsync"/> before
    /// any assets file reaches <see cref="OnFileFoundAsync"/>, so this dictionary is fully
    /// populated by the time assets files are processed. When multiple binlogs reference the
    /// same project (e.g., build and publish passes), their info is merged via
    /// <see cref="MSBuildProjectInfo.MergeWith"/> so that properties like
    /// <see cref="MSBuildProjectInfo.SelfContained"/> or <see cref="MSBuildProjectInfo.PublishAot"/>
    /// (typically only set in the publish pass) are available when processing the shared
    /// <c>project.assets.json</c>.
    /// </remarks>
    private readonly ConcurrentDictionary<string, MSBuildProjectInfo> projectInfoByAssetsFile = new(StringComparer.OrdinalIgnoreCase);

    // Source directory passed to BinLogProcessor for path rebasing.
    private string? sourceDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildBinaryLogComponentDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">Factory for creating component streams.</param>
    /// <param name="walkerFactory">Factory for directory walking.</param>
    /// <param name="commandLineInvocationService">Service for command line invocation.</param>
    /// <param name="directoryUtilityService">Service for directory operations.</param>
    /// <param name="fileUtilityService">Service for file operations.</param>
    /// <param name="pathUtilityService">Service for path operations.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public MSBuildBinaryLogComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IDirectoryUtilityService directoryUtilityService,
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        ILogger<MSBuildBinaryLogComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.binLogProcessor = new BinLogProcessor(logger);
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
        this.projectInfoProvider = new DotNetProjectInfoProvider(
            commandLineInvocationService,
            directoryUtilityService,
            fileUtilityService,
            pathUtilityService,
            logger);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildBinaryLogComponentDetector"/> class
    /// with an explicit <see cref="IBinLogProcessor"/> for testing.
    /// </summary>
    internal MSBuildBinaryLogComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IDirectoryUtilityService directoryUtilityService,
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        IBinLogProcessor binLogProcessor,
        ILogger<MSBuildBinaryLogComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.binLogProcessor = binLogProcessor;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
        this.projectInfoProvider = new DotNetProjectInfoProvider(
            commandLineInvocationService,
            directoryUtilityService,
            fileUtilityService,
            pathUtilityService,
            logger);
    }

    /// <inheritdoc />
    public override string Id => "MSBuildBinaryLog";

    /// <inheritdoc />
    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)!];

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = ["project.assets.json"];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.NuGet, ComponentType.DotNet];

    /// <inheritdoc />
    public override int Version { get; } = 1;

    /// <inheritdoc />
    public override Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.sourceDirectory = request.SourceDirectory.FullName;
        this.projectInfoProvider.Initialize(request.SourceDirectory.FullName, request.SourceFileRoot?.FullName);
        return base.ExecuteDetectorAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Eagerly discover and process all binlog files.
        // This completes before OnPrepareDetectionAsync returns, guaranteeing all project info
        // is indexed before any assets file reaches OnFileFoundAsync.
        var binlogStreams = this.ComponentStreamEnumerableFactory.GetComponentStreams(
            this.CurrentScanRequest.SourceDirectory,
            ["*.binlog"],
            this.CurrentScanRequest.DirectoryExclusionPredicate);

        foreach (var stream in binlogStreams)
        {
            this.ProcessBinlogFile(stream.Location);
        }

        // Phase 2: Return the original observable unchanged.
        // SearchPatterns only includes project.assets.json, so the observable already
        // contains only assets files — no filtering needed.
        return Task.FromResult(processRequests);
    }

    /// <inheritdoc />
    protected override Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default) =>

        // Only assets files reach here; binlogs are processed in OnPrepareDetectionAsync.
        this.ProcessAssetsFileAsync(processRequest, cancellationToken);

    /// <summary>
    /// Determines whether a project should be classified as development-only.
    /// A project is development-only if IsTestProject=true, IsShipping=false, or IsDevelopment=true.
    /// </summary>
    private static bool IsDevelopmentOnlyProject(MSBuildProjectInfo projectInfo) =>
        projectInfo.IsTestProject == true ||
        projectInfo.IsShipping == false ||
        projectInfo.IsDevelopment == true;

    /// <summary>
    /// Gets the IsDevelopmentDependency metadata override for a package from the specified items.
    /// </summary>
    /// <param name="items">The item dictionary to check (e.g., PackageReference or PackageDownload).</param>
    /// <param name="packageName">The package name to look up.</param>
    /// <returns>
    /// True if explicitly marked as a development dependency,
    /// false if explicitly marked as NOT a development dependency,
    /// null if no explicit override is set or the package is not in the items.
    /// </returns>
    private static bool? GetDevelopmentDependencyOverride(IDictionary<string, ITaskItem> items, string packageName)
    {
        if (items.TryGetValue(packageName, out var item))
        {
            var metadataValue = item.GetMetadata("IsDevelopmentDependency");
            if (!string.IsNullOrEmpty(metadataValue))
            {
                return string.Equals(metadataValue, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return null;
    }

    /// <summary>
    /// Maps the MSBuild OutputType property to "application" or "library".
    /// Returns <c>null</c> for empty/unknown values rather than guessing.
    /// </summary>
    private static string? GetTargetType(string? outputType)
    {
        if (string.IsNullOrEmpty(outputType))
        {
            return null;
        }

        // https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-options/output#outputtype
        if (outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
            outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase) ||
            outputType.Equals("AppContainerExe", StringComparison.OrdinalIgnoreCase))
        {
            return "application";
        }

        if (outputType.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
            outputType.Equals("Module", StringComparison.OrdinalIgnoreCase))
        {
            return "library";
        }

        return null;
    }

    /// <summary>
    /// Determines if a project is self-contained using MSBuild properties from the binlog.
    /// Reads the SelfContained and PublishAot properties directly.
    /// </summary>
    private static bool IsSelfContainedFromProjectInfo(MSBuildProjectInfo projectInfo) =>
        projectInfo.SelfContained == true || projectInfo.PublishAot == true;

    private void ProcessBinlogFile(string binlogPath)
    {
        var assetsFilesFound = new List<string>();

        try
        {
            this.Logger.LogDebug("Processing binlog file: {BinlogPath}", binlogPath);

            var projectInfos = this.binLogProcessor.ExtractProjectInfo(binlogPath, this.sourceDirectory);

            if (projectInfos.Count == 0)
            {
                this.Logger.LogInformation("No project information could be extracted from binlog: {BinlogPath}", binlogPath);
                return;
            }

            foreach (var projectInfo in projectInfos)
            {
                this.IndexProjectInfo(projectInfo, assetsFilesFound);
                this.LogMissingAssetsWarnings(projectInfo);
            }

            // Log summary warning if no assets files were found
            if (assetsFilesFound.Count == 0 && projectInfos.Count > 0)
            {
                this.Logger.LogWarning(
                    "Binlog {BinlogPath} contained {ProjectCount} project(s) but no project.assets.json files were referenced. NuGet restore may not have run.",
                    binlogPath,
                    projectInfos.Count);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to process binlog file: {BinlogPath}", binlogPath);
        }
    }

    private void IndexProjectInfo(MSBuildProjectInfo projectInfo, List<string> assetsFilesFound)
    {
        // Index by assets file path for lookup when processing lock files.
        // Use AddOrUpdate+MergeWith so that multiple binlogs for the same project
        // (e.g., build and publish passes) form a superset rather than keeping only the first.
        // Normalize to forward slashes so lookup from OS-native ComponentStream.Location matches.
        if (!string.IsNullOrEmpty(projectInfo.ProjectAssetsFile))
        {
            var normalizedAssetsPath = PathRebasingUtility.NormalizePath(projectInfo.ProjectAssetsFile);
            this.projectInfoByAssetsFile.AddOrUpdate(
                normalizedAssetsPath,
                _ => projectInfo,
                (_, existing) => existing.MergeWith(projectInfo));
            assetsFilesFound.Add(projectInfo.ProjectAssetsFile);
        }
    }

    private void LogMissingAssetsWarnings(MSBuildProjectInfo projectInfo)
    {
        if (string.IsNullOrEmpty(projectInfo.ProjectAssetsFile))
        {
            this.Logger.LogWarning(
                "No ProjectAssetsFile property found in binlog for project: {ProjectPath}. NuGet dependencies may not be detected.",
                projectInfo.ProjectPath);
        }
        else if (!this.fileUtilityService.Exists(projectInfo.ProjectAssetsFile))
        {
            this.Logger.LogWarning(
                "Project.assets.json referenced in binlog does not exist: {AssetsFile} (from project {ProjectPath})",
                projectInfo.ProjectAssetsFile,
                projectInfo.ProjectPath);
        }
    }

    /// <summary>
    /// Registers a DotNet component based on SDK version from the binlog.
    /// </summary>
    /// <remarks>
    /// This is equivalent to DotNetComponentDetector's behavior but uses the SDK version
    /// directly from the binlog (NETCoreSdkVersion property) rather than running `dotnet --version`.
    /// The binlog value is more accurate as it represents the actual SDK used during the build.
    ///
    /// For target type (application/library), we use the OutputType property from the binlog
    /// which is equivalent to what DotNetComponentDetector determines by inspecting the PE headers.
    ///
    /// When a lock file is available, self-contained detection uses both binlog properties
    /// (SelfContained, PublishAot) and the lock file heuristic (ILCompiler in libraries,
    /// runtime download dependencies matching framework references) for comprehensive coverage.
    /// </remarks>
    private void RegisterDotNetComponent(MSBuildProjectInfo projectInfo, LockFile? lockFile = null)
    {
        if (string.IsNullOrEmpty(projectInfo.NETCoreSdkVersion) || string.IsNullOrEmpty(projectInfo.ProjectPath))
        {
            return;
        }

        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectInfo.ProjectPath);

        // Determine target type from OutputType property.
        // Known application types: Exe, WinExe, AppContainerExe
        // Known library types: Library, Module
        // Unknown values are left as null (don't assume).
        var targetType = GetTargetType(projectInfo.OutputType);

        // Primary self-contained check from binlog properties (SelfContained, PublishAot)
        var isSelfContainedFromBinlog = IsSelfContainedFromProjectInfo(projectInfo);

        if (lockFile != null)
        {
            // When lock file is available, check per-target self-contained
            // combining binlog properties and lock file heuristics
            foreach (var target in lockFile.Targets)
            {
                var isSelfContained = isSelfContainedFromBinlog ||
                    LockFileUtilities.IsSelfContainedFromLockFile(lockFile.PackageSpec, target.TargetFramework, target);
                var projectType = LockFileUtilities.GetTargetTypeWithSelfContained(targetType, isSelfContained);
                var frameworkName = target.TargetFramework?.GetShortFolderName();

                singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new DotNetComponent(projectInfo.NETCoreSdkVersion, frameworkName, projectType)));
            }

            // If no targets in lock file, fall through to binlog-only registration below
            if (lockFile.Targets.Count > 0)
            {
                return;
            }
        }

        // Binlog-only path: no lock file available or no targets in lock file
        var projectTypeFromBinlog = LockFileUtilities.GetTargetTypeWithSelfContained(targetType, isSelfContainedFromBinlog);

        // Get target frameworks from binlog properties
        var targetFrameworks = new List<string>();
        if (!string.IsNullOrEmpty(projectInfo.TargetFramework))
        {
            targetFrameworks.Add(projectInfo.TargetFramework);
        }
        else if (!string.IsNullOrEmpty(projectInfo.TargetFrameworks))
        {
            targetFrameworks.AddRange(projectInfo.TargetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        // Register a DotNet component for each target framework
        if (targetFrameworks.Count > 0)
        {
            foreach (var framework in targetFrameworks)
            {
                singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new DotNetComponent(projectInfo.NETCoreSdkVersion, framework, projectTypeFromBinlog)));
            }
        }
        else
        {
            // No target framework info available, register with just SDK version
            singleFileComponentRecorder.RegisterUsage(
                new DetectedComponent(new DotNetComponent(projectInfo.NETCoreSdkVersion, targetFramework: null, projectTypeFromBinlog)));
        }
    }

    private async Task ProcessAssetsFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken)
    {
        var assetsFilePath = processRequest.ComponentStream.Location;

        try
        {
            var lockFile = this.lockFileFormat.Read(processRequest.ComponentStream.Stream, assetsFilePath);

            this.RecordLockfileVersion(lockFile.Version);

            if (lockFile.PackageSpec == null)
            {
                this.Logger.LogDebug("Lock file {LockFilePath} does not contain a PackageSpec.", assetsFilePath);
                return;
            }

            // Try to find matching binlog info
            var projectInfo = this.FindProjectInfoForAssetsFile(assetsFilePath);

            if (projectInfo != null)
            {
                // We have binlog info, use enhanced processing
                this.ProcessLockFileWithProjectInfo(lockFile, projectInfo, assetsFilePath);
            }
            else
            {
                // Fallback to standard processing without binlog info
                // This matches NuGetProjectModelProjectCentricComponentDetector + DotNetComponentDetector behavior
                this.Logger.LogDebug(
                    "No binlog information found for assets file: {AssetsFile}. Using fallback processing.",
                    assetsFilePath);
                await this.ProcessLockFileFallbackAsync(lockFile, assetsFilePath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to process NuGet lockfile: {LockFile}", assetsFilePath);
        }
    }

    /// <summary>
    /// Finds the <see cref="MSBuildProjectInfo"/> associated with the given assets file path.
    /// Paths are already rebased to the scanning machine by <see cref="IBinLogProcessor"/>.
    /// </summary>
    private MSBuildProjectInfo? FindProjectInfoForAssetsFile(string assetsFilePath)
    {
        // Normalize to forward slashes for consistent lookup.
        // BinLogProcessor stores keys with forward slashes, but ComponentStream.Location
        // uses OS-native separators (backslashes on Windows).
        var normalizedPath = PathRebasingUtility.NormalizePath(assetsFilePath);
        this.projectInfoByAssetsFile.TryGetValue(normalizedPath, out var projectInfo);
        return projectInfo;
    }

    /// <summary>
    /// Processes a lock file with enhanced project info from the binlog.
    /// </summary>
    /// <remarks>
    /// This method uses the same core logic as NuGetProjectModelProjectCentricComponentDetector:
    /// - Gets top-level libraries via GetTopLevelLibraries
    /// - Determines framework packages and dev dependencies
    /// - Navigates dependency graph via NavigateAndRegister
    /// - Registers PackageDownload dependencies
    ///
    /// Enhancements from binlog:
    /// - If project sets IsTestProject=true, IsShipping=false, or IsDevelopment=true,
    ///   all dependencies are marked as development dependencies.
    /// - Per-package IsDevelopmentDependency metadata overrides are applied transitively.
    /// </remarks>
    private void ProcessLockFileWithProjectInfo(LockFile lockFile, MSBuildProjectInfo projectInfo, string assetsFilePath)
    {
        var (explicitReferencedDependencies, explicitlyReferencedComponentIds) = LockFileUtilities.ResolveExplicitDependencies(lockFile, this.Logger);

        // Use project path from RestoreMetadata (consistent with NuGetProjectModelProjectCentricComponentDetector).
        // BinLogProcessor has already rebased projectInfo.ProjectPath to the scanning machine.
        // RestoreMetadata.ProjectPath comes from the lock file which is on the same machine as the assets file.
        // Fall back to the assets file path to avoid collisions when no project path is available.
        var recorderLocation = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath
            ?? projectInfo.ProjectPath
            ?? assetsFilePath;
        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(recorderLocation);

        // Get the project info for the target framework (use inner build if available)
        MSBuildProjectInfo GetProjectInfoForTarget(LockFileTarget target)
        {
            if (target.TargetFramework != null)
            {
                var innerBuild = projectInfo.InnerBuilds.FirstOrDefault(
                    ib => !string.IsNullOrEmpty(ib.TargetFramework) &&
                          NuGetFramework.Parse(ib.TargetFramework).Equals(target.TargetFramework));
                if (innerBuild != null)
                {
                    return innerBuild;
                }
            }

            return projectInfo;
        }

        foreach (var target in lockFile.Targets)
        {
            var targetProjectInfo = GetProjectInfoForTarget(target);
            var frameworkReferences = LockFileUtilities.GetFrameworkReferences(lockFile, target);
            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(target.TargetFramework, frameworkReferences, target);

            // Base logic: check if library is a framework component or dev dependency in lock file
            bool IsFrameworkOrDevDependency(LockFileTargetLibrary library) =>
                frameworkPackages.Any(fp => fp.IsAFrameworkComponent(library.Name, library.Version)) ||
                LockFileUtilities.IsADevelopmentDependency(library, lockFile);

            foreach (var dependency in explicitReferencedDependencies)
            {
                var library = target.GetTargetLibrary(dependency.Name);
                if (library?.Name == null)
                {
                    continue;
                }

                // Combine project-level and per-package overrides into a single value.
                // When set, this applies transitively to all dependencies of this package.
                var devDependencyOverride = IsDevelopmentOnlyProject(targetProjectInfo)
                    ? true
                    : GetDevelopmentDependencyOverride(targetProjectInfo.PackageReference, library.Name);

                LockFileUtilities.NavigateAndRegister(
                    target,
                    explicitlyReferencedComponentIds,
                    singleFileComponentRecorder,
                    library,
                    null,
                    devDependencyOverride.HasValue ? _ => devDependencyOverride.Value : IsFrameworkOrDevDependency);
            }
        }

        // Register PackageDownload dependencies with dev-dependency overrides
        LockFileUtilities.RegisterPackageDownloads(
            singleFileComponentRecorder,
            lockFile,
            (packageName, framework) => this.IsPackageDownloadDevDependency(packageName, framework, projectInfo));

        // Register DotNet component with combined binlog + lock file self-contained detection
        this.RegisterDotNetComponent(projectInfo, lockFile);
    }

    /// <summary>
    /// Determines if a PackageDownload is a development dependency based on project info.
    /// </summary>
    private bool IsPackageDownloadDevDependency(string packageName, NuGetFramework? framework, MSBuildProjectInfo projectInfo)
    {
        // Get the project info for this framework (use inner build if available)
        var targetProjectInfo = projectInfo;
        if (framework != null)
        {
            var innerBuild = projectInfo.InnerBuilds.FirstOrDefault(
                ib => !string.IsNullOrEmpty(ib.TargetFramework) &&
                      NuGetFramework.Parse(ib.TargetFramework).Equals(framework));
            if (innerBuild != null)
            {
                targetProjectInfo = innerBuild;
            }
        }

        // Project-level override: all deps are dev deps
        if (IsDevelopmentOnlyProject(targetProjectInfo))
        {
            return true;
        }

        // Check for explicit item metadata override
        var devOverride = GetDevelopmentDependencyOverride(targetProjectInfo.PackageDownload, packageName);
        if (devOverride.HasValue)
        {
            return devOverride.Value;
        }

        // Default: PackageDownload is a dev dependency
        return true;
    }

    /// <summary>
    /// Processes a lock file without binlog info (fallback mode).
    /// </summary>
    /// <remarks>
    /// This method exactly matches NuGetProjectModelProjectCentricComponentDetector's behavior
    /// to ensure no loss of information when binlog data is not available.
    /// </remarks>
    private async Task ProcessLockFileFallbackAsync(LockFile lockFile, string location, CancellationToken cancellationToken)
    {
        var projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath ?? location;
        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectPath);
        LockFileUtilities.ProcessLockFile(lockFile, singleFileComponentRecorder, this.Logger);

        // Register DotNet components (SDK version, target framework, project type)
        // This matches DotNetComponentDetector's behavior for the fallback path
        await this.projectInfoProvider.RegisterDotNetComponentsAsync(lockFile, location, this.ComponentRecorder, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task OnDetectionFinishedAsync()
    {
        this.projectInfoByAssetsFile.Clear();
        return Task.CompletedTask;
    }
}
