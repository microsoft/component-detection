namespace Microsoft.ComponentDetection.Detectors.Maven;
using System;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;

[Export(typeof(IMavenCommandService))]
public class MavenCommandService : IMavenCommandService
{
    internal const string PrimaryCommand = "mvn";

    internal const string MvnVersionArgument = "--version";

    internal static readonly string[] AdditionalValidCommands = new[] { "mvn.cmd" };

    [Import]
    public ICommandLineInvocationService CommandLineInvocationService { get; set; }

    [Import]
    public IMavenStyleDependencyGraphParserService ParserService { get; set; }

    [Import]
    public ILogger Logger { get; set; }

    public string BcdeMvnDependencyFileName => "bcde.mvndeps";

    public async Task<bool> MavenCLIExistsAsync()
    {
        return await this.CommandLineInvocationService.CanCommandBeLocatedAsync(PrimaryCommand, AdditionalValidCommands, MvnVersionArgument);
    }

    public async Task GenerateDependenciesFileAsync(ProcessRequest processRequest)
    {
        var pomFile = processRequest.ComponentStream;
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={this.BcdeMvnDependencyFileName}", "-DoutputType=text", $"-f{pomFile.Location}" };
        var result = await this.CommandLineInvocationService.ExecuteCommandAsync(PrimaryCommand, AdditionalValidCommands, cliParameters);
        if (result.ExitCode != 0)
        {
            this.Logger.LogVerbose($"Mvn execution failed for pom file: {pomFile.Location}");
            this.Logger.LogError(string.IsNullOrEmpty(result.StdErr) ? result.StdOut : result.StdErr);
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.ParserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }
}
