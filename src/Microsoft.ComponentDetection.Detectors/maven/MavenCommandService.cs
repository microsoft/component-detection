#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
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

    public async Task GenerateDependenciesFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken = default)
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

        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={this.BcdeMvnDependencyFileName}", "-DoutputType=text", $"-f{pomFile.Location}" };

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
        }
        else
        {
            this.logger.LogDebug("{DetectorPrefix}: Execution of \"dependency:tree\" on {PomFileLocation} completed successfully", DetectorLogPrefix, pomFile.Location);
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }
}
