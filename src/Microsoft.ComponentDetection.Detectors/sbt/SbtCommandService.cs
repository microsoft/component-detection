#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Sbt;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    internal const string SbtVersionArgument = "--version";

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
        var additionalCommands = new List<string>(AdditionalValidCommands);

        // On Windows, try to locate sbt via Coursier installation
        var coursierPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Coursier",
            "data",
            "bin",
            "sbt.bat");

        if (File.Exists(coursierPath))
        {
            additionalCommands.Add(coursierPath);
            this.logger.LogDebug("{DetectorPrefix}: Found sbt at Coursier path: {Path}", DetectorLogPrefix, coursierPath);
        }

        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(
            PrimaryCommand,
            additionalCommands,
            SbtVersionArgument);
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
        var cliParameters = new[] { "dependencyTree" };

        // Build additional commands list with Coursier path detection
        var additionalCommands = new List<string>(AdditionalValidCommands);
        var coursierPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Coursier",
            "data",
            "bin",
            "sbt.bat");

        if (File.Exists(coursierPath))
        {
            additionalCommands.Add(coursierPath);
            this.logger.LogDebug("{DetectorPrefix}: Using sbt from Coursier path: {Path}", DetectorLogPrefix, coursierPath);
        }

        var result = await this.commandLineInvocationService.ExecuteCommandAsync(
            PrimaryCommand,
            additionalCommands,
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

            // Save stdout to the sbtdeps file for parsing, removing [info] prefixes
            var sbtDepsPath = Path.Combine(buildDirectory.FullName, this.BcdeSbtDependencyFileName);
            try
            {
                // Clean SBT output: remove [info]/[warn]/[error] prefixes and Scala version suffixes
                // BUT keep tree structure characters (|, -, +) which are needed by the Maven parser
                var allLines = result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                var cleanedLines = allLines
                    .Select(line => Regex.Replace(line, @"\s*\[.\]$", string.Empty)) // Remove [S] or similar suffixes
                    .Select(line => Regex.Replace(line, @"^\[info\]\s*|\[warn\]\s*|\[error\]\s*", string.Empty))
                    .Select(line => Regex.Replace(line, @"_\d+\.\d+(?=:)", string.Empty)) // Remove Scala version suffix like _2.13:
                    .Where(line =>
                    {
                        var trimmed = line.Trim();

                        // Keep only lines that look like valid Maven coordinates
                        // Valid Maven coordinate pattern: optional tree chars then [group]:[artifact]:[version]...
                        // The group must contain at least one dot (standard Maven convention)
                        return Regex.IsMatch(trimmed, @"^[\s|\-+]*[a-z0-9\-_.]*\.[a-z0-9\-_.]+:[a-z0-9\-_.,]+:[a-z0-9\-_.]+");
                    })
                    .Select(line =>
                    {
                        // Extract just the coordinates part (after tree structure chars)
                        var coordinatesMatch = Regex.Match(line, @"([a-z0-9\-_.]*\.[a-z0-9\-_.]+:[a-z0-9\-_.,]+:[a-z0-9\-_.]+)");
                        if (coordinatesMatch.Success)
                        {
                            var coords = coordinatesMatch.Groups[1].Value;
                            var parts = coords.Split(':');
                            if (parts.Length == 3)
                            {
                                // Insert default packaging 'jar': group:artifact:jar:version
                                var mavenCoord = parts[0] + ":" + parts[1] + ":jar:" + parts[2];

                                // Find where the coordinates start in the original line and preserve tree structure
                                var treePrefix = line[..coordinatesMatch.Index];
                                return treePrefix + mavenCoord;
                            }
                        }

                        return line;
                    })
                    .ToList();

                var cleanedOutput = string.Join(Environment.NewLine, cleanedLines);
                this.logger.LogDebug("{DetectorPrefix}: Writing {LineCount} cleaned lines to {SbtDepsPath}", DetectorLogPrefix, cleanedLines.Count, sbtDepsPath);
                await File.WriteAllTextAsync(sbtDepsPath, cleanedOutput, cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to write SBT dependencies file at {Path}: {Exception}", sbtDepsPath, ex);
            }
        }
    }

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        using var sr = new StreamReader(processRequest.ComponentStream.Stream);

        var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
    }
}
