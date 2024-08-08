namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class PipCommandService : IPipCommandService
{
    private const string PipReportDisableFastDepsEnvVar = "PipReportDisableFastDeps";

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly IEnvironmentVariableService environmentService;
    private readonly ILogger<PipCommandService> logger;

    public PipCommandService()
    {
    }

    public PipCommandService(
        ICommandLineInvocationService commandLineInvocationService,
        IPathUtilityService pathUtilityService,
        IFileUtilityService fileUtilityService,
        IEnvironmentVariableService environmentService,
        ILogger<PipCommandService> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.pathUtilityService = pathUtilityService;
        this.fileUtilityService = fileUtilityService;
        this.environmentService = environmentService;
        this.logger = logger;
    }

    public async Task<bool> PipExistsAsync(string pipPath = null, string pythonPath = null)
    {
        var (pipExecutable, pythonExecutable) = await this.ResolvePipAsync(pipPath, pythonPath);
        return !string.IsNullOrEmpty(pipExecutable) || !string.IsNullOrEmpty(pythonExecutable);
    }

    public async Task<Version> GetPipVersionAsync(string pipPath = null, string pythonPath = null)
    {
        var (pipExecutable, pythonExecutable) = await this.ResolvePipAsync(pipPath, pythonPath);
        var command = await this.ExecuteCommandAsync(
            pipExecutable,
            pythonExecutable,
            null,
            parameters: "--version");

        if (command.ExitCode != 0)
        {
            this.logger.LogDebug("Failed to execute pip version with StdErr {StdErr}.", command.StdErr);
            return null;
        }

        try
        {
            // stdout will be in the format of "pip 20.0.2 from c:\python\lib\site-packages\pip (python 3.8)"
            var versionString = command.StdOut.Split(' ')[1];
            return Version.Parse(versionString);
        }
        catch (Exception)
        {
            this.logger.LogDebug("Failed to parse pip version with StdErr {StdErr}.", command.StdErr);
            return null;
        }
    }

    private async Task<(string PipExectuable, string PythonExecutable)> ResolvePipAsync(string pipPath = null, string pythonPath = null)
    {
        var pipCommand = string.IsNullOrEmpty(pipPath) ? "pip" : pipPath;
        var pythonCommand = string.IsNullOrEmpty(pythonPath) ? "python" : $"{pythonPath}";
        var pythonPipCommand = $"{pythonCommand} -m pip";

        if (await this.commandLineInvocationService.CanCommandBeLocatedAsync(pythonCommand, null, ["-m", "pip", "--version"]))
        {
            this.logger.LogDebug("Python Command succeeded: {PythonCmd}", pythonPipCommand);
            return (null, pythonCommand);
        }
        else if (await this.CanCommandBeLocatedAsync(pipCommand))
        {
            return (pipCommand, null);
        }

        return (null, null);
    }

    private async Task<bool> CanCommandBeLocatedAsync(string pipPath)
    {
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(pipPath, new List<string> { "pip3" }, "--version");
    }

    private async Task<CommandLineExecutionResult> ExecuteCommandAsync(
        string pipExecutable = null,
        string pythonExecutable = null,
        IEnumerable<string> additionalCandidateCommands = null,
        DirectoryInfo workingDirectory = null,
        CancellationToken cancellationToken = default,
        params string[] parameters)
    {
        if (!string.IsNullOrEmpty(pipExecutable))
        {
            return await this.commandLineInvocationService.ExecuteCommandAsync(pipExecutable, additionalCandidateCommands, workingDirectory, cancellationToken, parameters);
        }
        else
        {
            var pythonPipParams = new[] { "-m", "pip" };
            var parametersFull = pythonPipParams.Concat(parameters).ToArray();
            return await this.commandLineInvocationService.ExecuteCommandAsync(pythonExecutable, additionalCandidateCommands, workingDirectory, cancellationToken, parametersFull);
        }
    }

    public async Task<(PipInstallationReport Report, FileInfo ReportFile)> GenerateInstallationReportAsync(string path, string pipExePath = null, string pythonExePath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return (new PipInstallationReport(), null);
        }

        var (pipExecutable, pythonExecutable) = await this.ResolvePipAsync(pipExePath, pythonExePath);
        var formattedPath = this.pathUtilityService.NormalizePath(path);
        var workingDir = new DirectoryInfo(this.pathUtilityService.GetParentDirectory(formattedPath));

        CommandLineExecutionResult command;
        var reportName = Path.GetRandomFileName();
        var reportFile = new FileInfo(Path.Combine(workingDir.FullName, reportName));

        string pipReportCommand;
        if (path.EndsWith(".py"))
        {
            pipReportCommand = $"install -e .";
        }
        else if (path.EndsWith(".txt"))
        {
            pipReportCommand = "install -r requirements.txt";
        }
        else
        {
            // Failure case, but this shouldn't be hit since detection is only running
            // on setup.py and requirements.txt files.
            this.logger.LogDebug("PipReport: Unsupported file type for pip installation report: {Path}", path);
            return (new PipInstallationReport(), null);
        }

        // When PIP_INDEX_URL is set, we need to pass it as a parameter to pip install command.
        // This should be done before running detection by the build system, otherwise the detection
        // will default to the public PyPI index if not configured in pip defaults. Note this index URL may have credentials, we need to remove it when logging.
        pipReportCommand += $" --dry-run --ignore-installed --quiet --report {reportName}";
        if (this.environmentService.DoesEnvironmentVariableExist("PIP_INDEX_URL"))
        {
            pipReportCommand += $" --index-url {this.environmentService.GetEnvironmentVariable("PIP_INDEX_URL")}";
        }

        if (!this.environmentService.DoesEnvironmentVariableExist(PipReportDisableFastDepsEnvVar) || !this.environmentService.IsEnvironmentVariableValueTrue(PipReportDisableFastDepsEnvVar))
        {
            pipReportCommand += $" --use-feature=fast-deps";
        }

        this.logger.LogDebug("PipReport: Generating pip installation report for {Path} with command: {Command}", formattedPath, pipReportCommand.RemoveSensitiveInformation());
        command = await this.ExecuteCommandAsync(
            pipExecutable,
            pythonExecutable,
            null,
            workingDir,
            cancellationToken,
            pipReportCommand);

        if (command.ExitCode == -1 && cancellationToken.IsCancellationRequested)
        {
            var errorMessage = $"PipReport: Cancelled for file '{formattedPath}' with command '{pipReportCommand.RemoveSensitiveInformation()}'.";
            using var failureRecord = new PipReportFailureTelemetryRecord
            {
                ExitCode = command.ExitCode,
                StdErr = $"{errorMessage} {command.StdErr}",
            };

            this.logger.LogWarning("{Error}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        else if (command.ExitCode != 0)
        {
            using var failureRecord = new PipReportFailureTelemetryRecord
            {
                ExitCode = command.ExitCode,
                StdErr = command.StdErr,
            };

            this.logger.LogDebug("PipReport: Pip installation report error: {StdErr}", command.StdErr);
            throw new InvalidOperationException($"PipReport: Failed to generate pip installation report for file {path} with exit code {command.ExitCode}");
        }

        var reportOutput = await this.fileUtilityService.ReadAllTextAsync(reportFile);
        return (JsonConvert.DeserializeObject<PipInstallationReport>(reportOutput), reportFile);
    }
}
