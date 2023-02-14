namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public class BcdeDevCommandService : ServiceBase, IArgumentHandlingService
{
    private readonly IBcdeScanExecutionService bcdeScanExecutionService;

    public BcdeDevCommandService(IBcdeScanExecutionService bcdeScanExecutionService, ILogger logger)
    {
        this.bcdeScanExecutionService = bcdeScanExecutionService;
        this.Logger = logger;
    }

    public bool CanHandle(IScanArguments arguments)
    {
        return arguments is BcdeDevArguments;
    }

    public async Task<ScanResult> HandleAsync(IScanArguments arguments)
    {
        // Run BCDE with the given arguments
        var detectionArguments = arguments as BcdeArguments;

        var result = await this.bcdeScanExecutionService.ExecuteScanAsync(detectionArguments);
        var detectedComponents = result.ComponentsFound.ToList();
        foreach (var detectedComponent in detectedComponents)
        {
            Console.WriteLine(detectedComponent.Component.Id);
        }

        // TODO: Get vulnerabilities from GH Advisories
        return result;
    }
}
