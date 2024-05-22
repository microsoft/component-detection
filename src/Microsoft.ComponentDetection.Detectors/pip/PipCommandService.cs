namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class PipCommandService : IPipCommandService
{
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

    public async Task<bool> PipExistsAsync(string pipPath = null)
    {
        return !string.IsNullOrEmpty(await this.ResolvePipAsync(pipPath));
    }

    private async Task<string> ResolvePipAsync(string pipPath = null)
    {
        var pipCommand = string.IsNullOrEmpty(pipPath) ? "pip" : pipPath;

        if (await this.CanCommandBeLocatedAsync(pipCommand))
        {
            return pipCommand;
        }

        return null;
    }

    private async Task<bool> CanCommandBeLocatedAsync(string pipPath)
    {
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(pipPath, new List<string> { "pip3" }, "--version");
    }

    public async Task<(PipInstallationReport Report, FileInfo ReportFile)> GenerateInstallationReportAsync(string path, string pipExePath = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return (new PipInstallationReport(), null);
        }

        var pipExecutable = await this.ResolvePipAsync(pipExePath);
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
            return (new PipInstallationReport(), null);
        }

        // When PIP_INDEX_URL is set, we need to pass it as a parameter to pip install command.
        // This should be done before running detection by the build system, otherwise the detection
        // will default to the public PyPI index if not configured in pip defaults.
        pipReportCommand += $" --dry-run --ignore-installed --quiet --report {reportName}";
        if (this.environmentService.DoesEnvironmentVariableExist("PIP_INDEX_URL"))
        {
            pipReportCommand += $" --index-url {this.environmentService.GetEnvironmentVariable("PIP_INDEX_URL")}";
        }

        this.logger.LogDebug("PipReport: Generating pip installation report for {Path} with command: {Command}", formattedPath, pipReportCommand);
        command = await this.commandLineInvocationService.ExecuteCommandAsync(
            pipExecutable,
            null,
            workingDir,
            pipReportCommand);

        if (command.ExitCode != 0)
        {
            this.logger.LogWarning("PipReport: Failed to generate pip installation report for file {Path} with exit code {ExitCode}", path, command.ExitCode);
            this.logger.LogDebug("PipReport: Pip installation report error: {StdErr}", command.StdErr);

            using var failureRecord = new PipReportFailureTelemetryRecord
            {
                ExitCode = command.ExitCode,
                StdErr = command.StdErr,
            };

            return (new PipInstallationReport(), null);
        }

        var reportOutput = await this.fileUtilityService.ReadAllTextAsync(reportFile);
        return (JsonConvert.DeserializeObject<PipInstallationReport>(reportOutput), reportFile);
    }
}
