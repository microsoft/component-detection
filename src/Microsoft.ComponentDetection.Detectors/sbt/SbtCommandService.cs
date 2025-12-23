#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Sbt;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.Extensions.Logging;

public class SbtCommandService : ISbtCommandService
{
    private const string DetectorLogPrefix = "SbtCli detector";
    internal const string SbtCLIFileLevelTimeoutSecondsEnvVar = "SbtCLIFileLevelTimeoutSeconds";
    internal const string PrimaryCommand = "sbt";

    internal const string SbtVersionArgument = "sbtVersion";

    internal static readonly string[] AdditionalValidCommands = ["sbt.bat"];

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IMavenStyleDependencyGraphParserService parserService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly ILogger<SbtCommandService> logger;

    public SbtCommandService(
        ICommandLineInvocationService commandLineInvocationService,
        IMavenStyleDependencyGraphParserService parserService,
        IEnvironmentVariableService envVarService,
        ILogger<SbtCommandService> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.parserService = parserService;
        this.envVarService = envVarService;
        this.logger = logger;
    }

    public string BcdeSbtDependencyFileName => "bcde.sbtdeps";

    public async Task<bool> SbtCLIExistsAsync()
    {
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(PrimaryCommand, AdditionalValidCommands, SbtVersionArgument);
    }

    public async Task GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default)
    {
        var cliFileTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = -1;
        if (this.envVarService.DoesEnvironmentVariableExist(SbtCLIFileLevelTimeoutSecondsEnvVar)
                    && int.TryParse(this.envVarService.GetEnvironmentVariable(SbtCLIFileLevelTimeoutSecondsEnvVar), out timeoutSeconds)
                    && timeoutSeconds >= 0)
        {
            cliFileTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            this.logger.LogInformation("{DetectorPrefix}: {TimeoutVar} var was set to {TimeoutSeconds} seconds.", DetectorLogPrefix, SbtCLIFileLevelTimeoutSecondsEnvVar, timeoutSeconds);
        }

        var buildSbtFile = processRequest.ComponentStream;
        var buildDirectory = new DirectoryInfo(Path.GetDirectoryName(buildSbtFile.Location));
        this.logger.LogDebug("{DetectorPrefix}: Running \"dependencyTree\" on {BuildSbtLocation}", DetectorLogPrefix, buildSbtFile.Location);

        // SBT requires running from the project directory
        var cliParameters = new[] { $"\"dependencyTree; export compile:dependencyTree > {this.BcdeSbtDependencyFileName}\"" };

        var result = await this.commandLineInvocationService.ExecuteCommandAsync(
            PrimaryCommand,
            AdditionalValidCommands,
            workingDirectory: buildDirectory,
            cancellationToken: cliFileTimeout.Token,
            cliParameters);

        if (result.ExitCode != 0)
        {
            this.logger.LogDebug("{DetectorPrefix}: execution failed for build.sbt file: {BuildSbtLocation}", DetectorLogPrefix, buildSbtFile.Location);
            var errorMessage = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
            var isErrorMessagePopulated = !string.IsNullOrWhiteSpace(errorMessage);

            if (isErrorMessagePopulated)
            {
                this.logger.LogError("Sbt output: {SbtStdErr}", errorMessage);
                processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(buildSbtFile.Location);
            }

            if (timeoutSeconds != -1 && cliFileTimeout.IsCancellationRequested)
            {
                this.logger.LogWarning("{DetectorPrefix}: There was a timeout in {BuildSbtLocation} file. Increase it using {TimeoutVar} environment variable.", DetectorLogPrefix, buildSbtFile.Location, SbtCLIFileLevelTimeoutSecondsEnvVar);
            }
        }
        else
        {
            this.logger.LogDebug("{DetectorPrefix}: Execution of \"dependencyTree\" on {BuildSbtLocation} completed successfully", DetectorLogPrefix, buildSbtFile.Location);
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }
}
