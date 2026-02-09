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

public class MavenCommandService : IMavenCommandService
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
        return await this.GenerateDependenciesFileAsync(processRequest, this.BcdeMvnDependencyFileName, cancellationToken);
    }

    public async Task<MavenCliResult> GenerateDependenciesFileAsync(ProcessRequest processRequest, string outputFileName, CancellationToken cancellationToken = default)
    {
        var pomFile = processRequest.ComponentStream;

        // Use semaphore to prevent concurrent Maven CLI executions for the same pom.xml.
        // This allows MvnCliComponentDetector and MavenWithFallbackDetector to safely share the output file.
        // Note: We don't pass the cancellation token to WaitAsync so that we can still check the cache
        // even if cancellation is requested. The cancellation will be honored during the actual CLI execution.
        var semaphore = this.locationLocks.GetOrAdd(pomFile.Location, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(CancellationToken.None);
        try
        {
            // Check if another detector already generated the deps file for this location
            if (this.completedLocations.TryGetValue(pomFile.Location, out var cachedResult))
            {
                this.logger.LogDebug("{DetectorPrefix}: Skipping duplicate \"dependency:tree\" for {PomFileLocation}, already generated", DetectorLogPrefix, pomFile.Location);
                return cachedResult;
            }

            var result = await this.GenerateDependenciesFileCoreAsync(processRequest, outputFileName, cancellationToken);
            this.completedLocations.TryAdd(pomFile.Location, result);
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<MavenCliResult> GenerateDependenciesFileCoreAsync(ProcessRequest processRequest, string outputFileName, CancellationToken cancellationToken)
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

        string[] cliParameters = ["dependency:tree", "-B", $"-DoutputFile={outputFileName}", "-DoutputType=text", $"-f{pomFile.Location}"];

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
}
