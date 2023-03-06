﻿namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class BcdeScanCommandService : IArgumentHandlingService
{
    public const string ManifestRelativePath = "ScanManifest_{timestamp}.json";

    private readonly IFileWritingService fileWritingService;
    private readonly IBcdeScanExecutionService bcdeScanExecutionService;
    private readonly ILogger<BcdeScanCommandService> logger;

    public BcdeScanCommandService(
        IFileWritingService fileWritingService,
        IBcdeScanExecutionService bcdeScanExecutionService,
        ILogger<BcdeScanCommandService> logger)
    {
        this.fileWritingService = fileWritingService;
        this.bcdeScanExecutionService = bcdeScanExecutionService;
        this.logger = logger;
    }

    public bool CanHandle(IScanArguments arguments)
    {
        return arguments is BcdeArguments;
    }

    public async Task<ScanResult> HandleAsync(IScanArguments arguments)
    {
        var bcdeArguments = (BcdeArguments)arguments;
        var result = await this.bcdeScanExecutionService.ExecuteScanAsync(bcdeArguments);
        this.WriteComponentManifest(bcdeArguments, result);
        return result;
    }

    private void WriteComponentManifest(IDetectionArguments detectionArguments, ScanResult scanResult)
    {
        FileInfo userRequestedManifestPath = null;

        if (detectionArguments.ManifestFile != null)
        {
            this.logger.LogInformation("Scan Manifest file: {ManifestFile}", detectionArguments.ManifestFile.FullName);
            userRequestedManifestPath = detectionArguments.ManifestFile;
        }
        else
        {
            this.logger.LogInformation("Scan Manifest file: {ManifestFile}", this.fileWritingService.ResolveFilePath(ManifestRelativePath));
        }

        var manifestJson = JsonConvert.SerializeObject(scanResult, Formatting.Indented);

        if (userRequestedManifestPath == null)
        {
            this.fileWritingService.AppendToFile(ManifestRelativePath, manifestJson);
        }
        else
        {
            this.fileWritingService.WriteFile(userRequestedManifestPath, manifestJson);
        }

        if (detectionArguments.PrintManifest)
        {
            Console.WriteLine(manifestJson);
        }
    }
}
