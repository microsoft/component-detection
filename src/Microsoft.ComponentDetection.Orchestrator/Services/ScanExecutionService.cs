namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.Logging;

public class ScanExecutionService : IScanExecutionService
{
    private readonly IEnumerable<IComponentDetector> detectors;
    private readonly IDetectorProcessingService detectorProcessingService;
    private readonly IDetectorRestrictionService detectorRestrictionService;
    private readonly IGraphTranslationService graphTranslationService;
    private readonly ILogger<ScanExecutionService> logger;

    public ScanExecutionService(
        IEnumerable<IComponentDetector> detectors,
        IDetectorProcessingService detectorProcessingService,
        IDetectorRestrictionService detectorRestrictionService,
        IGraphTranslationService graphTranslationService,
        ILogger<ScanExecutionService> logger)
    {
        this.detectors = detectors;
        this.detectorProcessingService = detectorProcessingService;
        this.detectorRestrictionService = detectorRestrictionService;
        this.graphTranslationService = graphTranslationService;
        this.logger = logger;
    }

    public async Task<ScanResult> ExecuteScanAsync(ScanSettings settings)
    {
        using var scope = this.logger.BeginScope("Executing BCDE scan");

        var detectorRestrictions = this.GetDetectorRestrictions(settings);
        var detectors = this.detectors.ToList();
        var detectorsWithAppliedRestrictions = this.detectorRestrictionService.ApplyRestrictions(detectorRestrictions, detectors).ToImmutableList();
        detectorRestrictions.DisabledDetectors = detectors.Except(detectorsWithAppliedRestrictions).ToList();

        this.logger.LogDebug("Finished applying restrictions to detectors.");

        var processingResult = await this.detectorProcessingService.ProcessDetectorsAsync(settings, detectorsWithAppliedRestrictions, detectorRestrictions);
        var scanResult = this.graphTranslationService.GenerateScanResultFromProcessingResult(processingResult, settings);

        scanResult.DetectorsInScan = detectorsWithAppliedRestrictions.Select(ConvertToContract).ToList();
        scanResult.DetectorsNotInScan = detectorRestrictions.DisabledDetectors.Select(ConvertToContract).ToList();
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

    private DetectorRestrictions GetDetectorRestrictions(ScanSettings settings)
    {
        var detectorRestrictions = new DetectorRestrictions
        {
            AllowedDetectorIds = settings.DetectorsFilter,
            AllowedDetectorCategories = settings.DetectorCategories,
        };

        if (settings.DetectorArgs != null && settings.DetectorArgs.Any())
        {
            var allEnabledDetectorIds = settings.DetectorArgs.Where(x => string.Equals("EnableIfDefaultOff", x.Value, StringComparison.OrdinalIgnoreCase) || string.Equals("Enable", x.Value, StringComparison.OrdinalIgnoreCase));
            detectorRestrictions.ExplicitlyEnabledDetectorIds = new HashSet<string>(allEnabledDetectorIds.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        }

        return detectorRestrictions;
    }
}
