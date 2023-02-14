namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public class DetectorListingCommandService : ServiceBase, IArgumentHandlingService
{
    private readonly IEnumerable<IComponentDetector> detectors;

    public DetectorListingCommandService(
        IEnumerable<IComponentDetector> detectors,
        ILogger logger)
    {
        this.detectors = detectors;
        this.Logger = logger;
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
        foreach (var detector in this.detectors)
        {
            this.Logger.LogInfo($"{detector.Id}");
        }

        return await Task.FromResult(ProcessingResultCode.Success);
    }
}
