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
    private readonly ILogger<ScanCommand> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanCommand"/> class.
    /// </summary>
    /// <param name="fileWritingService">The file writing service.</param>
    /// <param name="scanExecutionService">The scan execution service.</param>
    /// <param name="logger">The logger.</param>
    public ScanCommand(
        IFileWritingService fileWritingService,
        IScanExecutionService scanExecutionService,
        ILogger<ScanCommand> logger)
    {
        this.fileWritingService = fileWritingService;
        this.scanExecutionService = scanExecutionService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ScanSettings settings)
    {
        this.fileWritingService.Init(settings.Output);
        var result = await this.scanExecutionService.ExecuteScanAsync(settings);
        this.WriteComponentManifest(settings, result);
        return 0;
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
            using var jsonWriter = new JsonTextWriter(Console.Out);
            var serializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
            };
            serializer.Serialize(jsonWriter, scanResult);
        }
    }
}
