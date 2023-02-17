namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.Extensions.Logging;

public class DetectorListingCommandService : ServiceBase, IArgumentHandlingService
{
    private readonly IEnumerable<IComponentDetector> detectors;

    public DetectorListingCommandService(
        IEnumerable<IComponentDetector> detectors,
        ILogger<DetectorListingCommandService> logger)
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
        this.Logger.LogInformation("Detectors: {DetectorList}", string.Join(',', this.detectors.Select(x => x.Id)));

        return await Task.FromResult(ProcessingResultCode.Success);
    }
}
