﻿namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

[Export(typeof(IArgumentHandlingService))]
public class BcdeDevCommandService : ServiceBase, IArgumentHandlingService
{
    public BcdeDevCommandService()
    {
    }

    public BcdeDevCommandService(IBcdeScanExecutionService bcdeScanExecutionService, ILogger logger)
    {
        this.BcdeScanExecutionService = bcdeScanExecutionService;
        this.Logger = logger;
    }

    [Import]
    public IBcdeScanExecutionService BcdeScanExecutionService { get; set; }

    public bool CanHandle(IScanArguments arguments)
    {
        return arguments is BcdeDevArguments;
    }

    public async Task<ScanResult> HandleAsync(IScanArguments arguments)
    {
        // Run BCDE with the given arguments
        var detectionArguments = arguments as BcdeArguments;

        var result = await this.BcdeScanExecutionService.ExecuteScanAsync(detectionArguments);
        var detectedComponents = result.ComponentsFound.ToList();
        foreach (var detectedComponent in detectedComponents)
        {
            Console.WriteLine(detectedComponent.Component.Id);
        }

        // TODO: Get vulnerabilities from GH Advisories
        return result;
    }
}
