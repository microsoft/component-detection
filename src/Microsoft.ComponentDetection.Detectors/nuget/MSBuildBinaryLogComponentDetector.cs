namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.Frameworks;
using global::NuGet.ProjectModel;
using Microsoft.Build.Framework;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
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
    private readonly LockFileFormat lockFileFormat = new();

    // Track which assets files have been processed to avoid duplicate processing
    private readonly ConcurrentDictionary<string, bool> processedAssetsFiles = new(StringComparer.OrdinalIgnoreCase);

    // Store project information extracted from binlogs keyed by project path
    private readonly ConcurrentDictionary<string, MSBuildProjectInfo> projectInfoByProjectPath = new(StringComparer.OrdinalIgnoreCase);

    // Store project information extracted from binlogs keyed by assets file path
    private readonly ConcurrentDictionary<string, MSBuildProjectInfo> projectInfoByAssetsFile = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildBinaryLogComponentDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">Factory for creating component streams.</param>
    /// <param name="walkerFactory">Factory for directory walking.</param>
    /// <param name="fileUtilityService">Service for file operations.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public MSBuildBinaryLogComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<MSBuildBinaryLogComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.binLogProcessor = new BinLogProcessor(logger);
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override string Id => "MSBuildBinaryLog";

    /// <inheritdoc />
    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)!];

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = ["*.binlog", "project.assets.json"];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.NuGet, ComponentType.DotNet];

    /// <inheritdoc />
    public override int Version { get; } = 1;

    /// <inheritdoc />
    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        // Collect all requests and sort them so binlogs are processed first
        // This ensures we have project info available when processing assets files
        var allRequests = await processRequests.ToList();

        this.Logger.LogDebug("Preparing detection: collected {Count} files", allRequests.Count);

        // Separate binlogs and assets files
        var binlogRequests = allRequests
            .Where(r => r.ComponentStream.Location.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var assetsRequests = allRequests
            .Where(r => r.ComponentStream.Location.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        this.Logger.LogDebug("Found {BinlogCount} binlog files and {AssetsCount} assets files", binlogRequests.Count, assetsRequests.Count);

        // Return binlogs first, then assets files
        var orderedRequests = binlogRequests.Concat(assetsRequests);

        return orderedRequests.ToObservable();
    }

    /// <inheritdoc />
    protected override Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        var fileExtension = Path.GetExtension(processRequest.ComponentStream.Location);

        if (fileExtension.Equals(".binlog", StringComparison.OrdinalIgnoreCase))
        {
            this.ProcessBinlogFile(processRequest);
        }
        else if (processRequest.ComponentStream.Location.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase))
        {
            this.ProcessAssetsFile(processRequest);
        }

        return Task.CompletedTask;
    }

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

    private void ProcessBinlogFile(ProcessRequest processRequest)
    {
        var binlogPath = processRequest.ComponentStream.Location;
        var assetsFilesFound = new List<string>();

        try
        {
            this.Logger.LogDebug("Processing binlog file: {BinlogPath}", binlogPath);

            var projectInfos = this.binLogProcessor.ExtractProjectInfo(binlogPath);

            if (projectInfos.Count == 0)
            {
                this.Logger.LogInformation("No project information could be extracted from binlog: {BinlogPath}", binlogPath);
                return;
            }

            foreach (var projectInfo in projectInfos)
            {
                this.IndexProjectInfo(projectInfo, assetsFilesFound);
                this.RegisterDotNetComponent(projectInfo);
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
        // Store the project info for later use when processing assets files
        var projectPath = projectInfo.ProjectPath;
        if (!string.IsNullOrEmpty(projectPath))
        {
            this.projectInfoByProjectPath.TryAdd(projectPath, projectInfo);
        }

        // Also index by assets file path for direct lookup
        if (!string.IsNullOrEmpty(projectInfo.ProjectAssetsFile))
        {
            this.projectInfoByAssetsFile.TryAdd(projectInfo.ProjectAssetsFile, projectInfo);
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
    /// </remarks>
    private void RegisterDotNetComponent(MSBuildProjectInfo projectInfo)
    {
        if (string.IsNullOrEmpty(projectInfo.NETCoreSdkVersion) || string.IsNullOrEmpty(projectInfo.ProjectPath))
        {
            return;
        }

        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectInfo.ProjectPath);

        // Determine target type from OutputType property
        // This is equivalent to DotNetComponentDetector's IsApplication check via PE headers
        string? targetType = null;
        if (!string.IsNullOrEmpty(projectInfo.OutputType))
        {
            targetType = projectInfo.OutputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                        projectInfo.OutputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase)
                ? "application"
                : "library";
        }

        // Get target frameworks - equivalent to iterating lockFile.Targets in DotNetComponentDetector
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
        // This matches DotNetComponentDetector's loop over lockFile.Targets
        if (targetFrameworks.Count > 0)
        {
            foreach (var framework in targetFrameworks)
            {
                var dotNetComponent = new DotNetComponent(projectInfo.NETCoreSdkVersion, framework, targetType);
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(dotNetComponent));
            }
        }
        else
        {
            // No target framework info available, register with just SDK version
            var dotNetComponent = new DotNetComponent(projectInfo.NETCoreSdkVersion, targetFramework: null, targetType);
            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(dotNetComponent));
        }
    }

    private void ProcessAssetsFile(ProcessRequest processRequest)
    {
        var assetsFilePath = processRequest.ComponentStream.Location;

        // Check if this assets file was already processed
        if (this.processedAssetsFiles.ContainsKey(assetsFilePath))
        {
            this.Logger.LogDebug("Assets file already processed: {AssetsFile}", assetsFilePath);
            return;
        }

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
            var projectInfo = this.FindProjectInfoForAssetsFile(assetsFilePath, lockFile);

            // Mark as processed
            this.processedAssetsFiles.TryAdd(assetsFilePath, true);

            if (projectInfo != null)
            {
                // We have binlog info, use enhanced processing
                this.ProcessLockFileWithProjectInfo(lockFile, projectInfo);
            }
            else
            {
                // Fallback to standard processing without binlog info
                // This matches NuGetProjectModelProjectCentricComponentDetector's behavior exactly
                this.Logger.LogDebug(
                    "No binlog information found for assets file: {AssetsFile}. Using fallback processing.",
                    assetsFilePath);
                this.ProcessLockFileFallback(lockFile, assetsFilePath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to process NuGet lockfile: {LockFile}", assetsFilePath);
        }
    }

    private MSBuildProjectInfo? FindProjectInfoForAssetsFile(string assetsFilePath, LockFile lockFile)
    {
        // Try to find by assets file path first
        if (this.projectInfoByAssetsFile.TryGetValue(assetsFilePath, out var infoByAssets))
        {
            return infoByAssets;
        }

        // Try to find by project path from the lock file
        var projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath;
        if (!string.IsNullOrEmpty(projectPath) &&
            this.projectInfoByProjectPath.TryGetValue(projectPath, out var infoByProject))
        {
            return infoByProject;
        }

        return null;
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
    private void ProcessLockFileWithProjectInfo(LockFile lockFile, MSBuildProjectInfo projectInfo)
    {
        var explicitReferencedDependencies = LockFileUtilities.GetTopLevelLibraries(lockFile)
            .Select(x => LockFileUtilities.GetLibraryComponentWithDependencyLookup(lockFile.Libraries, x.Name, x.Version, x.VersionRange, this.Logger))
            .Where(x => x != null)
            .ToList();

        var explicitlyReferencedComponentIds = explicitReferencedDependencies
            .Select(x => new NuGetComponent(x!.Name, x.Version.ToNormalizedString()).Id)
            .ToHashSet();

        // Use project path from RestoreMetadata (consistent with NuGetProjectModelProjectCentricComponentDetector)
        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(
            lockFile.PackageSpec?.RestoreMetadata?.ProjectPath ?? projectInfo.ProjectPath ?? string.Empty);

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
                var library = target.GetTargetLibrary(dependency!.Name);
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
    private void ProcessLockFileFallback(LockFile lockFile, string location)
    {
        var explicitReferencedDependencies = LockFileUtilities.GetTopLevelLibraries(lockFile)
            .Select(x => LockFileUtilities.GetLibraryComponentWithDependencyLookup(lockFile.Libraries, x.Name, x.Version, x.VersionRange, this.Logger))
            .Where(x => x != null)
            .ToList();

        var explicitlyReferencedComponentIds = explicitReferencedDependencies
            .Select(x => new NuGetComponent(x!.Name, x.Version.ToNormalizedString()).Id)
            .ToHashSet();

        // Use project path from RestoreMetadata (consistent with NuGetProjectModelProjectCentricComponentDetector)
        var projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath ?? location;
        var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectPath);

        foreach (var target in lockFile.Targets)
        {
            var frameworkReferences = LockFileUtilities.GetFrameworkReferences(lockFile, target);
            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(target.TargetFramework, frameworkReferences, target);

            // Same logic as NuGetProjectModelProjectCentricComponentDetector.IsFrameworkOrDevelopmentDependency
            bool IsFrameworkOrDevDependency(LockFileTargetLibrary library) =>
                frameworkPackages.Any(fp => fp.IsAFrameworkComponent(library.Name, library.Version)) ||
                LockFileUtilities.IsADevelopmentDependency(library, lockFile);

            foreach (var library in explicitReferencedDependencies.Select(x => target.GetTargetLibrary(x!.Name)).Where(x => x != null))
            {
                LockFileUtilities.NavigateAndRegister(
                    target,
                    explicitlyReferencedComponentIds,
                    singleFileComponentRecorder,
                    library!,
                    null,
                    IsFrameworkOrDevDependency);
            }
        }

        // Register PackageDownload dependencies (same as NuGetProjectModelProjectCentricComponentDetector)
        LockFileUtilities.RegisterPackageDownloads(singleFileComponentRecorder, lockFile);
    }
}
