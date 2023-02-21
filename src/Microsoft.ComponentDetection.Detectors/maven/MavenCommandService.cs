namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.Extensions.Logging;

public class MavenCommandService : IMavenCommandService
{
    internal const string PrimaryCommand = "mvn";

    internal const string MvnVersionArgument = "--version";

    internal static readonly string[] AdditionalValidCommands = new[] { "mvn.cmd" };

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IMavenStyleDependencyGraphParserService parserService;
    private readonly ILogger<MavenCommandService> logger;

    public MavenCommandService(
        ICommandLineInvocationService commandLineInvocationService,
        IMavenStyleDependencyGraphParserService parserService,
        ILogger<MavenCommandService> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.parserService = parserService;
        this.logger = logger;
    }

    public string BcdeMvnDependencyFileName => "bcde.mvndeps";

    public async Task<bool> MavenCLIExistsAsync()
    {
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(PrimaryCommand, AdditionalValidCommands, MvnVersionArgument);
    }

    public async Task GenerateDependenciesFileAsync(ProcessRequest processRequest)
    {
        var pomFile = processRequest.ComponentStream;
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={this.BcdeMvnDependencyFileName}", "-DoutputType=text", $"-f{pomFile.Location}" };
        var result = await this.commandLineInvocationService.ExecuteCommandAsync(PrimaryCommand, AdditionalValidCommands, cliParameters);
        if (result.ExitCode != 0)
        {
            this.logger.LogDebug("Mvn execution failed for pom file: {PomFileLocation}", pomFile.Location);
            this.logger.LogError("Mvn output: {MvnStdErr}", string.IsNullOrEmpty(result.StdErr) ? result.StdOut : result.StdErr);
            processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(pomFile.Location);
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }
}
