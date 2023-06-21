namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.Logging;

public class BcdeScanExecutionService : IBcdeScanExecutionService
{
    private readonly IEnumerable<IComponentDetector> detectors;
    private readonly IDetectorProcessingService detectorProcessingService;
    private readonly IDetectorRestrictionService detectorRestrictionService;
    private readonly IGraphTranslationService graphTranslationService;
    private readonly ILogger<BcdeScanExecutionService> logger;

    public BcdeScanExecutionService(
        IEnumerable<IComponentDetector> detectors,
        IDetectorProcessingService detectorProcessingService,
        IDetectorRestrictionService detectorRestrictionService,
        IGraphTranslationService graphTranslationService,
        ILogger<BcdeScanExecutionService> logger)
    {
        this.detectors = detectors;
        this.detectorProcessingService = detectorProcessingService;
        this.detectorRestrictionService = detectorRestrictionService;
        this.graphTranslationService = graphTranslationService;
        this.logger = logger;
    }

    public async Task<ScanResult> ExecuteScanAsync(IDetectionArguments detectionArguments)
    {
        using var scope = this.logger.BeginScope("Executing BCDE scan");

        var detectorRestrictions = this.GetDetectorRestrictions(detectionArguments);
        var detectors = this.detectorRestrictionService.ApplyRestrictions(detectorRestrictions, this.detectors).ToImmutableList();

        this.logger.LogDebug("Finished applying restrictions to detectors.");

        var processingResult = await this.detectorProcessingService.ProcessDetectorsAsync(detectionArguments, detectors, detectorRestrictions);
        var scanResult = this.graphTranslationService.GenerateScanResultFromProcessingResult(processingResult, detectionArguments);

        scanResult.DetectorsInScan = detectors.Select(x => ConvertToContract(x)).ToList();
        scanResult.ResultCode = processingResult.ResultCode;

        return scanResult;
    }

    private static Detector ConvertToContract(IComponentDetector detector)
    {
        return new Detector
        {
            DetectorId = detector.Id,
            IsExperimental = detector is IExperimentalDetector,
            Version = detector.Version,
            SupportedComponentTypes = detector.SupportedComponentTypes,
        };
    }

    private DetectorRestrictions GetDetectorRestrictions(IDetectionArguments detectionArguments)
    {
        var detectorRestrictions = new DetectorRestrictions
        {
            AllowedDetectorIds = detectionArguments.DetectorsFilter,
            AllowedDetectorCategories = detectionArguments.DetectorCategories,
        };

        if (detectionArguments.DetectorArgs != null && detectionArguments.DetectorArgs.Any())
        {
            var args = ArgumentHelper.GetDetectorArgs(detectionArguments.DetectorArgs);
            var allEnabledDetectorIds = args.Where(x => string.Equals("EnableIfDefaultOff", x.Value, StringComparison.OrdinalIgnoreCase) || string.Equals("Enable", x.Value, StringComparison.OrdinalIgnoreCase));
            detectorRestrictions.ExplicitlyEnabledDetectorIds = new HashSet<string>(allEnabledDetectorIds.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        }

        return detectorRestrictions;
    }
}
