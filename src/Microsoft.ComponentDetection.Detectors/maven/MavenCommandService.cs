#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.Extensions.Logging;

internal class MavenCommandService : IMavenCommandService
{
    private const string DetectorLogPrefix = "MvnCli detector";
    internal const string MvnCLIFileLevelTimeoutSecondsEnvVar = "MvnCLIFileLevelTimeoutSeconds";
    internal const string PrimaryCommand = "mvn";

    internal const string MvnVersionArgument = "--version";

    internal static readonly string[] AdditionalValidCommands = ["mvn.cmd"];

    /// <summary>
    /// Per-location semaphores to prevent concurrent Maven CLI executions for the same pom.xml.
    /// This allows multiple detectors (e.g., MvnCliComponentDetector and MavenWithFallbackDetector)
    /// to safely share the same output file without race conditions.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locationLocks = new();

    /// <summary>
    /// Tracks locations where dependency generation has completed successfully.
    /// Used to skip duplicate executions when multiple detectors process the same pom.xml.
    /// </summary>
    private readonly ConcurrentDictionary<string, MavenCliResult> completedLocations = new();

    /// <summary>
    /// Tracks the number of active readers for each dependency file.
    /// Used for safe file cleanup coordination between detectors.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> fileReaderCounts = new();

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IMavenStyleDependencyGraphParserService parserService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly ILogger<MavenCommandService> logger;

    public MavenCommandService(
        ICommandLineInvocationService commandLineInvocationService,
        IMavenStyleDependencyGraphParserService parserService,
        IEnvironmentVariableService envVarService,
        ILogger<MavenCommandService> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.parserService = parserService;
        this.envVarService = envVarService;
        this.logger = logger;
    }

    public string BcdeMvnDependencyFileName => "bcde.mvndeps";

    public async Task<bool> MavenCLIExistsAsync()
    {
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(PrimaryCommand, AdditionalValidCommands, MvnVersionArgument);
    }

    public async Task<MavenCliResult> GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default)
    {
        var pomFile = processRequest.ComponentStream;
        var pomDir = Path.GetDirectoryName(pomFile.Location);
        var depsFilePath = Path.Combine(pomDir, this.BcdeMvnDependencyFileName);

        // Register as file reader immediately to prevent premature cleanup
        this.RegisterFileReader(depsFilePath);

        // Check the cache before acquiring the semaphore to allow fast-path returns
        // even when cancellation has been requested.
        if (this.completedLocations.TryGetValue(pomFile.Location, out var cachedResult)
            && cachedResult.Success
            && File.Exists(depsFilePath))
        {
            this.logger.LogDebug("{DetectorPrefix}: Skipping duplicate \"dependency:tree\" for {PomFileLocation}, already generated", DetectorLogPrefix, pomFile.Location);
            return cachedResult;
        }

        // Use semaphore to prevent concurrent Maven CLI executions for the same pom.xml.
        // This allows MvnCliComponentDetector and MavenWithFallbackDetector to safely share the output file.
        var semaphore = this.locationLocks.GetOrAdd(pomFile.Location, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Re-check the cache after acquiring the semaphore in case another caller
            // completed while we were waiting.
            if (this.completedLocations.TryGetValue(pomFile.Location, out cachedResult)
                && cachedResult.Success
                && File.Exists(depsFilePath))
            {
                this.logger.LogDebug("{DetectorPrefix}: Skipping duplicate \"dependency:tree\" for {PomFileLocation}, already generated", DetectorLogPrefix, pomFile.Location);
                return cachedResult;
            }

            var result = await this.GenerateDependenciesFileCoreAsync(processRequest, cancellationToken);

            // Only cache successful results. Failed results should allow retries for transient failures,
            // and caching them would waste memory since the cache check requires Success == true anyway.
            if (result.Success)
            {
                this.completedLocations[pomFile.Location] = result;
            }

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<MavenCliResult> GenerateDependenciesFileCoreAsync(ProcessRequest processRequest, CancellationToken cancellationToken)
    {
        var cliFileTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = -1;
        if (this.envVarService.DoesEnvironmentVariableExist(MvnCLIFileLevelTimeoutSecondsEnvVar)
                    && int.TryParse(this.envVarService.GetEnvironmentVariable(MvnCLIFileLevelTimeoutSecondsEnvVar), out timeoutSeconds)
                    && timeoutSeconds >= 0)
        {
            cliFileTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            this.logger.LogInformation("{DetectorPrefix}: {TimeoutVar} var was set to {TimeoutSeconds} seconds.", DetectorLogPrefix, MvnCLIFileLevelTimeoutSecondsEnvVar, timeoutSeconds);
        }

        var pomFile = processRequest.ComponentStream;
        this.logger.LogDebug("{DetectorPrefix}: Running \"dependency:tree\" on {PomFileLocation}", DetectorLogPrefix, pomFile.Location);

        string[] cliParameters = ["dependency:tree", "-B", $"-DoutputFile={this.BcdeMvnDependencyFileName}", "-DoutputType=text", $"-f{pomFile.Location}"];

        var result = await this.commandLineInvocationService.ExecuteCommandAsync(PrimaryCommand, AdditionalValidCommands, cancellationToken: cliFileTimeout.Token, cliParameters);

        if (result.ExitCode != 0)
        {
            this.logger.LogDebug("{DetectorPrefix}: execution failed for pom file: {PomFileLocation}", DetectorLogPrefix, pomFile.Location);
            var errorMessage = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
            var isErrorMessagePopulated = !string.IsNullOrWhiteSpace(errorMessage);

            if (isErrorMessagePopulated)
            {
                this.logger.LogError("Mvn output: {MvnStdErr}", errorMessage);
                processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(pomFile.Location);
            }

            if (timeoutSeconds != -1 && cliFileTimeout.IsCancellationRequested)
            {
                this.logger.LogWarning("{DetectorPrefix}: There was a timeout in {PomFileLocation} file. Increase it using {TimeoutVar} environment variable.", DetectorLogPrefix, pomFile.Location, MvnCLIFileLevelTimeoutSecondsEnvVar);
            }

            return new MavenCliResult(false, errorMessage);
        }
        else
        {
            this.logger.LogDebug("{DetectorPrefix}: Execution of \"dependency:tree\" on {PomFileLocation} completed successfully", DetectorLogPrefix, pomFile.Location);

            return new MavenCliResult(true, null);
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }

    /// <summary>
    /// Registers that a detector is actively reading a dependency file.
    /// This prevents premature deletion by other detectors.
    /// </summary>
    /// <param name="dependencyFilePath">The path to the dependency file being read.</param>
    public void RegisterFileReader(string dependencyFilePath)
    {
        this.fileReaderCounts.AddOrUpdate(dependencyFilePath, 1, (key, count) => count + 1);
        this.logger.LogDebug(
            "Registered file reader for {DependencyFilePath}, count: {Count}",
            dependencyFilePath,
            this.fileReaderCounts[dependencyFilePath]);
    }

    /// <summary>
    /// Unregisters a detector's active reading of a dependency file and attempts cleanup.
    /// If no other detectors are reading the file, it will be safely deleted.
    /// </summary>
    /// <param name="dependencyFilePath">The path to the dependency file that was being read.</param>
    /// <param name="detectorId">The identifier of the detector unregistering the file reader.</param>
    public void UnregisterFileReader(string dependencyFilePath, string detectorId = null)
    {
        var newCount = this.fileReaderCounts.AddOrUpdate(dependencyFilePath, 0, (key, count) => Math.Max(0, count - 1));
        this.logger.LogDebug(
            "{DetectorId}: Unregistered file reader for {DependencyFilePath}, count: {Count}",
            detectorId ?? "Unknown",
            dependencyFilePath,
            newCount);

        // If no readers remain, attempt cleanup
        if (newCount == 0)
        {
            this.TryDeleteDependencyFileIfNotInUse(dependencyFilePath, detectorId);
        }
    }

    /// <summary>
    /// Attempts to delete a dependency file if no detectors are currently using it.
    /// </summary>
    /// <param name="dependencyFilePath">The path to the dependency file to delete.</param>
    /// <param name="detectorId">The identifier of the detector requesting the deletion.</param>
    private void TryDeleteDependencyFileIfNotInUse(string dependencyFilePath, string detectorId = null)
    {
        var detector = detectorId ?? "Unknown";

        // Safe to delete - no readers are using the file (count was already verified to be 0)
        try
        {
            if (File.Exists(dependencyFilePath))
            {
                File.Delete(dependencyFilePath);
                this.logger.LogDebug("{DetectorId}: Successfully deleted dependency file {DependencyFilePath}", detector, dependencyFilePath);
            }
            else
            {
                this.logger.LogDebug("{DetectorId}: Dependency file {DependencyFilePath} was already deleted", detector, dependencyFilePath);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "{DetectorId}: Failed to delete dependency file {DependencyFilePath}", detector, dependencyFilePath);
        }
    }
}
