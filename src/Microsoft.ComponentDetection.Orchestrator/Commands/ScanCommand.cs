namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Spectre.Console.Cli;

/// <summary>
/// Executes a scan and writes the result to a file.
/// </summary>
public sealed class ScanCommand : AsyncCommand<ScanSettings>
{
    private const string ManifestRelativePath = "ScanManifest_{timestamp}.json";
    private readonly IFileWritingService fileWritingService;
    private readonly IScanExecutionService scanExecutionService;
    private readonly IComponentDetectionConfigFileService componentDetectionConfigFileService;
    private readonly ILogger<ScanCommand> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanCommand"/> class.
    /// </summary>
    /// <param name="fileWritingService">The file writing service.</param>
    /// <param name="scanExecutionService">The scan execution service.</param>
    /// <param name="componentDetectionConfigFileService">The component detection config file service.</param>
    /// <param name="logger">The logger.</param>
    public ScanCommand(
        IFileWritingService fileWritingService,
        IScanExecutionService scanExecutionService,
        IComponentDetectionConfigFileService componentDetectionConfigFileService,
        ILogger<ScanCommand> logger)
    {
        this.fileWritingService = fileWritingService;
        this.scanExecutionService = scanExecutionService;
        this.componentDetectionConfigFileService = componentDetectionConfigFileService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ScanSettings settings)
    {
        this.fileWritingService.Init(settings.Output);
        await this.componentDetectionConfigFileService.InitAsync(settings.SourceDirectory.FullName);
        var result = await this.scanExecutionService.ExecuteScanAsync(settings);
        this.WriteComponentManifest(settings, result);
        return 0;
    }

    /// <summary>
    /// Method to provide a way to execute the scan command and obtain the ScanResult object.
    /// </summary>
    /// <param name="settings">ScanSettings object specifying the parameters for the scan execution.</param>
    /// <returns>A ScanResult object.</returns>
    public async Task<ScanResult> ExecuteScanCommandAsync(ScanSettings settings)
    {
        this.fileWritingService.Init(settings.Output);
        var result = await this.scanExecutionService.ExecuteScanAsync(settings);
        this.WriteComponentManifest(settings, result);
        return result;
    }

    private void WriteComponentManifest(ScanSettings settings, ScanResult scanResult)
    {
        FileInfo userRequestedManifestPath = null;

        if (settings.ManifestFile != null)
        {
            this.logger.LogInformation("Scan Manifest file: {ManifestFile}", settings.ManifestFile.FullName);
            userRequestedManifestPath = settings.ManifestFile;
        }
        else
        {
            this.logger.LogInformation("Scan Manifest file: {ManifestFile}", this.fileWritingService.ResolveFilePath(ManifestRelativePath));
        }

        if (userRequestedManifestPath == null)
        {
            this.fileWritingService.AppendToFile(ManifestRelativePath, scanResult);
        }
        else
        {
            this.fileWritingService.WriteFile(userRequestedManifestPath, scanResult);
        }

        if (settings.PrintManifest)
        {
            var jsonWriter = new JsonTextWriter(Console.Out);
            var serializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
            };
            serializer.Serialize(jsonWriter, scanResult);
        }
    }
}
