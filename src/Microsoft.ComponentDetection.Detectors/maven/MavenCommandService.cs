using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using System;
using System.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    [Export(typeof(IMavenCommandService))]
    public class MavenCommandService : IMavenCommandService
    {
        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IMavenStyleDependencyGraphParserService ParserService { get; set; }

        [Import]
        public ILogger Logger { get; set; }

        public string BcdeMvnDependencyFileName => "bcde.mvndeps";

        internal const string PrimaryCommand = "mvn";

        internal const string MvnVersionArgument = "--version";

        internal static readonly string[] AdditionalValidCommands = new[] { "mvn.cmd" };

        public async Task<bool> MavenCLIExists()
        {
            return await CommandLineInvocationService.CanCommandBeLocated(PrimaryCommand, AdditionalValidCommands, MvnVersionArgument);
        }

        public async Task GenerateDependenciesFile(ProcessRequest processRequest)
        {
            var pomFile = processRequest.ComponentStream;
            var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={BcdeMvnDependencyFileName}", "-DoutputType=text", $"-f{pomFile.Location}" };
            var result = await CommandLineInvocationService.ExecuteCommand(PrimaryCommand, AdditionalValidCommands, cliParameters);
            if (result.ExitCode != 0)
            {
                Logger.LogVerbose($"Mvn execution failed for pom file: {pomFile.Location}");
                Logger.LogError(string.IsNullOrEmpty(result.StdErr) ? result.StdOut : result.StdErr);
            }
        }

        public void ParseDependenciesFile(ProcessRequest processRequest)
        {
            using StreamReader sr = new StreamReader(processRequest.ComponentStream.Stream);

            var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            ParserService.Parse(lines, processRequest.SingleFileComponentRecorder);
        }
    }
}
