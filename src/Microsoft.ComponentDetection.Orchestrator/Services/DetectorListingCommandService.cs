namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.Extensions.Logging;

public class DetectorListingCommandService : IArgumentHandlingService
{
    private readonly IEnumerable<IComponentDetector> detectors;
    private readonly ILogger<DetectorListingCommandService> logger;

    public DetectorListingCommandService(
        IEnumerable<IComponentDetector> detectors,
        ILogger<DetectorListingCommandService> logger)
    {
        this.detectors = detectors;
        this.logger = logger;
    }

    public bool CanHandle(IScanArguments arguments)
    {
        return arguments is ListDetectionArgs;
    }

    public async Task<ScanResult> HandleAsync(IScanArguments arguments)
    {
        await this.ListDetectorsAsync(arguments as IListDetectionArgs);
        return new ScanResult()
        {
            ResultCode = ProcessingResultCode.Success,
        };
    }

    private async Task<ProcessingResultCode> ListDetectorsAsync(IScanArguments listArguments)
    {
        this.logger.LogInformation("Detectors:");

        foreach (var detector in this.detectors)
        {
            this.logger.LogInformation("{DetectorId}", detector.Id);
        }

        return await Task.FromResult(ProcessingResultCode.Success);
    }
}
