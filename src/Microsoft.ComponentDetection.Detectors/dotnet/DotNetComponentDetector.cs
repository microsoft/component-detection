namespace Microsoft.ComponentDetection.Detectors.DotNet;

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.ProjectModel;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class DotNetComponentDetector : FileComponentDetector
{
    private const string GlobalJsonFileName = "global.json";
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IDirectoryUtilityService directoryUtilityService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly LockFileFormat lockFileFormat = new();
    private readonly ConcurrentDictionary<string, string?> sdkVersionCache = [];
    private readonly JsonDocumentOptions jsonDocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip };
    private string? sourceDirectory;
    private string? sourceFileRootDirectory;

    public DotNetComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IDirectoryUtilityService directoryUtilityService,
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<DotNetComponentDetector> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.directoryUtilityService = directoryUtilityService;
        this.fileUtilityService = fileUtilityService;
        this.pathUtilityService = pathUtilityService;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "DotNet";

    public override IList<string> SearchPatterns { get; } = [LockFileFormat.AssetsFileName];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DotNet];

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => ["DotNet"];

    [return: NotNullIfNotNull(nameof(path))]
    private string? NormalizeDirectory(string? path) => string.IsNullOrEmpty(path) ? path : Path.TrimEndingDirectorySeparator(this.pathUtilityService.NormalizePath(path));

    /// <summary>
    /// Given a path under sourceDirectory, and the same path in another filesystem,
    /// determine what path could be replaced with sourceDirectory.
    /// </summary>
    /// <param name="sourceDirectoryBasedPath">Some directory path under sourceDirectory, including sourceDirectory.</param>
    /// <param name="rebasePath">Path to the same directory as <paramref name="sourceDirectoryBasedPath"/> but in a different root.</param>
    /// <returns>Portion of <paramref name="rebasePath"/> that corresponds to root, or null if it can not be rebased.</returns>
    private string? GetRootRebasePath(string sourceDirectoryBasedPath, string? rebasePath)
    {
        if (string.IsNullOrEmpty(rebasePath) || string.IsNullOrEmpty(this.sourceDirectory) || string.IsNullOrEmpty(sourceDirectoryBasedPath))
        {
            return null;
        }

        // sourceDirectory is normalized, normalize others
        sourceDirectoryBasedPath = this.NormalizeDirectory(sourceDirectoryBasedPath);
        rebasePath = this.NormalizeDirectory(rebasePath);

        // nothing to do if the paths are the same
        if (rebasePath.Equals(sourceDirectoryBasedPath, StringComparison.Ordinal))
        {
            return null;
        }

        // find the relative path under sourceDirectory.
        var sourceDirectoryRelativePath = this.NormalizeDirectory(Path.GetRelativePath(this.sourceDirectory!, sourceDirectoryBasedPath));

        this.Logger.LogDebug("Attempting to rebase {RebasePath} to {SourceDirectoryBasedPath} using relative {SourceDirectoryRelativePath}", rebasePath, sourceDirectoryBasedPath, sourceDirectoryRelativePath);

        // if the rebase path has the same relative portion, then we have a replacement.
        if (rebasePath.EndsWith(sourceDirectoryRelativePath))
        {
            return rebasePath[..^sourceDirectoryRelativePath.Length];
        }

        // The path didn't have a common relative path, it might have been copied from a completely different location since it was built.
        // We cannot rebase the paths.
        return null;
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
                this.Logger.LogDebug("Failed to invoke 'dotnet --version'. Return: {Return} StdErr: {StdErr} StdOut: {StdOut}.", process.ExitCode, process.StdErr, process.StdOut);
                return null;
            }

            return process.StdOut.Trim();
        }
        catch (InvalidOperationException ioe)
        {
            // debug only - it could be that dotnet is not actually on the path and specified directly by the build scripts.
            this.Logger.LogDebug("Failed to invoke 'dotnet --version'. {Message}", ioe.Message);
            return null;
        }
    }

    public override Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.sourceDirectory = this.NormalizeDirectory(request.SourceDirectory.FullName);
        this.sourceFileRootDirectory = this.NormalizeDirectory(request.SourceFileRoot?.FullName);

        return base.ExecuteDetectorAsync(request, cancellationToken);
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var lockFile = this.lockFileFormat.Read(processRequest.ComponentStream.Stream, processRequest.ComponentStream.Location);

        if (lockFile.PackageSpec is null || lockFile.PackageSpec.RestoreMetadata is null)
        {
            // The lock file is not valid, or does not contain a PackageSpec.
            // This could be due to the lock file being generated by a different version of the SDK.
            // We should not fail the detector, but we should log a warning.
            this.Logger.LogWarning("Lock file {LockFilePath} does not contain project information.", processRequest.ComponentStream.Location);
            return;
        }

        var projectAssetsDirectory = this.pathUtilityService.GetParentDirectory(processRequest.ComponentStream.Location);
        var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
        var projectOutputPath = lockFile.PackageSpec.RestoreMetadata.OutputPath;

        // The output path should match the location that the assets file, if it doesn't we could be analyzing paths
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
            this.Logger.LogWarning("Project path {ProjectPath} specified by {ProjectAssetsPath} does not exist.", projectPath, processRequest.ComponentStream.Location);
        }

        var projectDirectory = this.pathUtilityService.GetParentDirectory(projectPath);
        var sdkVersion = await this.GetSdkVersionAsync(projectDirectory, cancellationToken);

        var projectName = lockFile.PackageSpec.RestoreMetadata.ProjectName;

        if (!this.directoryUtilityService.Exists(projectOutputPath))
        {
            this.Logger.LogWarning("Project output path {ProjectOutputPath} specified by {ProjectAssetsPath} does not exist.", projectOutputPath, processRequest.ComponentStream.Location);

            // default to use the location of the assets file.
            projectOutputPath = projectAssetsDirectory;
        }

        var targetType = this.GetProjectType(projectOutputPath, projectName, cancellationToken);

        var componentReporter = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectPath);
        foreach (var target in lockFile.Targets ?? [])
        {
            var targetFramework = target.TargetFramework?.GetShortFolderName();

            componentReporter.RegisterUsage(new DetectedComponent(new DotNetComponent(sdkVersion, targetFramework, targetType)));
        }
    }

    private string? GetProjectType(string projectOutputPath, string projectName, CancellationToken cancellationToken)
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
                        this.Logger.LogWarning("Failed to open output assembly {AssemblyPath} error {Message}.", candidate, e.Message);
                    }
                }
            }
            catch (IOException e)
            {
                this.Logger.LogWarning("Failed to enumerate output directory {OutputPath} error {Message}.", projectOutputPath, e.Message);
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
    /// <param name="cancellationToken">Cancellation token to halt the search.</param>
    /// <returns>Sdk version found, or null if no version can be detected.</returns>
    private async Task<string?> GetSdkVersionAsync(string? projectDirectory, CancellationToken cancellationToken)
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
                var recorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(globalJsonPath);
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
            sdkVersion = await this.GetSdkVersionAsync(parentDirectory, cancellationToken);
        }

        this.sdkVersionCache[projectDirectory] = sdkVersion;

        return sdkVersion;
    }
}
