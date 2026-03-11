namespace Microsoft.ComponentDetection.Detectors.DotNet;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.ProjectModel;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using NuGetLockFileUtilities = Microsoft.ComponentDetection.Detectors.NuGet.LockFileUtilities;

/// <summary>
/// Resolves DotNet project and SDK information from the environment.
/// Handles SDK version resolution, project type detection, path rebasing,
/// and DotNet component registration. Used by both DotNetComponentDetector
/// and MSBuildBinaryLogComponentDetector.
/// </summary>
internal class DotNetProjectInfoProvider
{
    private const string GlobalJsonFileName = "global.json";

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IDirectoryUtilityService directoryUtilityService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, string?> sdkVersionCache = [];
    private readonly JsonDocumentOptions jsonDocumentOptions =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private string? sourceDirectory;
    private string? sourceFileRootDirectory;

    public DotNetProjectInfoProvider(
        ICommandLineInvocationService commandLineInvocationService,
        IDirectoryUtilityService directoryUtilityService,
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        ILogger logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.directoryUtilityService = directoryUtilityService;
        this.fileUtilityService = fileUtilityService;
        this.pathUtilityService = pathUtilityService;
        this.logger = logger;
    }

    /// <summary>
    /// Initializes source directory paths for path rebasing. Call once per scan.
    /// </summary>
    public void Initialize(string? sourceDirectory, string? sourceFileRootDirectory)
    {
        this.sourceDirectory = this.NormalizeDirectory(sourceDirectory);
        this.sourceFileRootDirectory = this.NormalizeDirectory(sourceFileRootDirectory);
    }

    /// <summary>
    /// Registers DotNet components from a lock file, determining SDK version and project type.
    /// This is the complete DotNet component registration logic shared between DotNetComponentDetector
    /// and MSBuildBinaryLogComponentDetector's fallback path.
    /// </summary>
    /// <param name="lockFile">The lock file to analyze.</param>
    /// <param name="assetsFileLocation">The location of the project.assets.json file.</param>
    /// <param name="componentRecorder">The component recorder to register components with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RegisterDotNetComponentsAsync(
        LockFile lockFile,
        string assetsFileLocation,
        IComponentRecorder componentRecorder,
        CancellationToken cancellationToken)
    {
        if (lockFile.PackageSpec?.RestoreMetadata is null)
        {
            return;
        }

        var projectAssetsDirectory = this.pathUtilityService.GetParentDirectory(assetsFileLocation);
        var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
        var projectOutputPath = lockFile.PackageSpec.RestoreMetadata.OutputPath;

        // The output path should match the location of the assets file, if it doesn't we could be analyzing paths
        // on a different filesystem root than they were created.
        // Attempt to rebase paths based on the difference between this file's location and the output path.
        var rebasePath = this.GetRootRebasePath(projectAssetsDirectory, projectOutputPath);

        if (rebasePath is not null)
        {
            projectPath = Path.Combine(this.sourceDirectory!, Path.GetRelativePath(rebasePath, projectPath));
            projectOutputPath = Path.Combine(this.sourceDirectory!, Path.GetRelativePath(rebasePath, projectOutputPath));
        }

        if (!this.fileUtilityService.Exists(projectPath))
        {
            // Could be the assets file was not actually from this build
            this.logger.LogWarning("Project path {ProjectPath} specified by {ProjectAssetsPath} does not exist.", projectPath, assetsFileLocation);
        }

        var projectDirectory = this.pathUtilityService.GetParentDirectory(projectPath);
        var sdkVersion = await this.GetSdkVersionAsync(projectDirectory, componentRecorder, cancellationToken);

        var projectName = lockFile.PackageSpec.RestoreMetadata.ProjectName;

        if (!this.directoryUtilityService.Exists(projectOutputPath))
        {
            this.logger.LogWarning("Project output path {ProjectOutputPath} specified by {ProjectAssetsPath} does not exist.", projectOutputPath, assetsFileLocation);

            // default to use the location of the assets file.
            projectOutputPath = projectAssetsDirectory;
        }

        var targetType = this.GetProjectType(projectOutputPath, projectName);

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(projectPath);
        foreach (var target in lockFile.Targets ?? [])
        {
            var targetFramework = target.TargetFramework;
            var isSelfContained = NuGetLockFileUtilities.IsSelfContainedFromLockFile(lockFile.PackageSpec, targetFramework, target);
            var targetTypeWithSelfContained = NuGetLockFileUtilities.GetTargetTypeWithSelfContained(targetType, isSelfContained);

            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(new DotNetComponent(sdkVersion, targetFramework?.GetShortFolderName(), targetTypeWithSelfContained)));
        }
    }

    [return: NotNullIfNotNull(nameof(path))]
    private string? NormalizeDirectory(string? path) => PathRebasingUtility.NormalizeDirectory(path);

    /// <summary>
    /// Given a path under sourceDirectory, and the same path in another filesystem,
    /// determine what path could be replaced with sourceDirectory.
    /// </summary>
    /// <param name="sourceDirectoryBasedPath">Some directory path under sourceDirectory, including sourceDirectory.</param>
    /// <param name="rebasePath">Path to the same directory as <paramref name="sourceDirectoryBasedPath"/> but in a different root.</param>
    /// <returns>Portion of <paramref name="rebasePath"/> that corresponds to root, or null if it can not be rebased.</returns>
    internal string? GetRootRebasePath(string sourceDirectoryBasedPath, string? rebasePath)
    {
        var result = PathRebasingUtility.GetRebaseRoot(this.sourceDirectory, sourceDirectoryBasedPath, rebasePath);

        if (result != null)
        {
            this.logger.LogDebug(
                "Rebasing paths from {RebasePath} to {SourceDirectoryBasedPath}",
                rebasePath,
                sourceDirectoryBasedPath);
        }

        return result;
    }

    internal string? GetProjectType(string projectOutputPath, string projectName)
    {
        if (this.directoryUtilityService.Exists(projectOutputPath) &&
            projectName is not null &&
            projectName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1)
        {
            try
            {
                // look for the compiled output, first as dll then as exe.
                var candidates = this.directoryUtilityService.EnumerateFiles(projectOutputPath, projectName + ".dll", SearchOption.AllDirectories)
                        .Concat(this.directoryUtilityService.EnumerateFiles(projectOutputPath, projectName + ".exe", SearchOption.AllDirectories));
                foreach (var candidate in candidates)
                {
                    try
                    {
                        return this.IsApplication(candidate) ? "application" : "library";
                    }
                    catch (Exception e)
                    {
                        this.logger.LogWarning("Failed to open output assembly {AssemblyPath} error {Message}.", candidate, e.Message);
                    }
                }
            }
            catch (IOException e)
            {
                this.logger.LogWarning("Failed to enumerate output directory {OutputPath} error {Message}.", projectOutputPath, e.Message);
            }
        }

        return null;
    }

    private bool IsApplication(string assemblyPath)
    {
        using var peReader = new PEReader(this.fileUtilityService.MakeFileStream(assemblyPath));

        // despite the name `IsExe` this is actually based of the CoffHeader Characteristics
        return peReader.PEHeaders.IsExe;
    }

    /// <summary>
    /// Recursively get the sdk version from the project directory or parent directories.
    /// </summary>
    /// <param name="projectDirectory">Directory to start the search.</param>
    /// <param name="componentRecorder">Component recorder for registering global.json components.</param>
    /// <param name="cancellationToken">Cancellation token to halt the search.</param>
    /// <returns>Sdk version found, or null if no version can be detected.</returns>
    internal async Task<string?> GetSdkVersionAsync(string? projectDirectory, IComponentRecorder componentRecorder, CancellationToken cancellationToken)
    {
        // normalize since we need to use as a key
        projectDirectory = this.NormalizeDirectory(projectDirectory);

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            // not expected
            return null;
        }

        if (this.sdkVersionCache.TryGetValue(projectDirectory, out var sdkVersion))
        {
            return sdkVersion;
        }

        var parentDirectory = this.pathUtilityService.GetParentDirectory(projectDirectory);
        var globalJsonPath = Path.Combine(projectDirectory, GlobalJsonFileName);

        if (this.fileUtilityService.Exists(globalJsonPath))
        {
            sdkVersion = await this.RunDotNetVersionAsync(projectDirectory, cancellationToken);

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                var globalJson = await JsonDocument.ParseAsync(this.fileUtilityService.MakeFileStream(globalJsonPath), cancellationToken: cancellationToken, options: this.jsonDocumentOptions).ConfigureAwait(false);
                if (globalJson.RootElement.TryGetProperty("sdk", out var sdk))
                {
                    if (sdk.TryGetProperty("version", out var version))
                    {
                        sdkVersion = version.GetString();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(sdkVersion))
            {
                var globalJsonComponent = new DetectedComponent(new DotNetComponent(sdkVersion));
                var recorder = componentRecorder.CreateSingleFileComponentRecorder(globalJsonPath);
                recorder.RegisterUsage(globalJsonComponent, isExplicitReferencedDependency: true);
                return sdkVersion;
            }

            // global.json may be malformed, or the sdk version may not be specified.
        }

        if (projectDirectory.Equals(this.sourceDirectory, StringComparison.OrdinalIgnoreCase) ||
            projectDirectory.Equals(this.sourceFileRootDirectory, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(parentDirectory) ||
            projectDirectory.Equals(parentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // if we are at the source directory, source file root, or have reached a root directory, run `dotnet --version`
            // this could fail if dotnet is not on the path, or if the global.json is malformed
            sdkVersion = await this.RunDotNetVersionAsync(projectDirectory, cancellationToken);
        }
        else
        {
            // recurse up the directory tree
            sdkVersion = await this.GetSdkVersionAsync(parentDirectory, componentRecorder, cancellationToken);
        }

        this.sdkVersionCache[projectDirectory] = sdkVersion;

        return sdkVersion;
    }

    private async Task<string?> RunDotNetVersionAsync(string workingDirectoryPath, CancellationToken cancellationToken)
    {
        var workingDirectory = new DirectoryInfo(workingDirectoryPath);

        try
        {
            var process = await this.commandLineInvocationService.ExecuteCommandAsync("dotnet", ["dotnet.exe"], workingDirectory, cancellationToken, "--version").ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                // debug only - it could be that dotnet is not actually on the path and specified directly by the build scripts.
                this.logger.LogDebug("Failed to invoke 'dotnet --version'. Return: {Return} StdErr: {StdErr} StdOut: {StdOut}.", process.ExitCode, process.StdErr, process.StdOut);
                return null;
            }

            return process.StdOut.Trim();
        }
        catch (InvalidOperationException ioe)
        {
            // debug only - it could be that dotnet is not actually on the path and specified directly by the build scripts.
            this.logger.LogDebug("Failed to invoke 'dotnet --version'. {Message}", ioe.Message);
            return null;
        }
    }
}
