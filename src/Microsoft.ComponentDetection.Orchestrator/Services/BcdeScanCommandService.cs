namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Newtonsoft.Json;

[Export(typeof(IArgumentHandlingService))]
public class BcdeScanCommandService : ServiceBase, IArgumentHandlingService
{
    public const string ManifestRelativePath = "ScanManifest_{timestamp}.json";

    public BcdeScanCommandService()
    {
    }

    public BcdeScanCommandService(
        IFileWritingService fileWritingService,
        IBcdeScanExecutionService bcdeScanExecutionService,
        ILogger logger)
    {
        this.FileWritingService = fileWritingService;
        this.BcdeScanExecutionService = bcdeScanExecutionService;
        this.Logger = logger;
    }

    [Import]
    public IFileWritingService FileWritingService { get; set; }

    [Import]
    public IBcdeScanExecutionService BcdeScanExecutionService { get; set; }

    public bool CanHandle(IScanArguments arguments)
    {
        return arguments is BcdeArguments;
    }

    public async Task<ScanResult> HandleAsync(IScanArguments arguments)
    {
        var bcdeArguments = (BcdeArguments)arguments;
        var result = await this.BcdeScanExecutionService.ExecuteScanAsync(bcdeArguments);
        this.WriteComponentManifest(bcdeArguments, result);
        return result;
    }

    private void WriteComponentManifest(IDetectionArguments detectionArguments, ScanResult scanResult)
    {
        FileInfo userRequestedManifestPath = null;

        if (detectionArguments.ManifestFile != null)
        {
            this.Logger.LogInfo($"Scan Manifest file: {detectionArguments.ManifestFile.FullName}");
            userRequestedManifestPath = detectionArguments.ManifestFile;
        }
        else
        {
            this.Logger.LogInfo($"Scan Manifest file: {this.FileWritingService.ResolveFilePath(ManifestRelativePath)}");
        }

        if (userRequestedManifestPath == null)
        {
            this.FileWritingService.AppendToFile(ManifestRelativePath, JsonConvert.SerializeObject(scanResult, Formatting.Indented));
        }
        else
        {
            this.FileWritingService.WriteFile(userRequestedManifestPath, JsonConvert.SerializeObject(scanResult, Formatting.Indented));
        }
    }
}
