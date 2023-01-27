namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;

[Export(typeof(IBcdeScanExecutionService))]
public class BcdeScanExecutionService : ServiceBase, IBcdeScanExecutionService
{
    [Import]
    public IDetectorRegistryService DetectorRegistryService { get; set; }

    [Import]
    public IDetectorProcessingService DetectorProcessingService { get; set; }

    [Import]
    public IDetectorRestrictionService DetectorRestrictionService { get; set; }

    [ImportMany]
    public IEnumerable<Lazy<IGraphTranslationService, GraphTranslationServiceMetadata>> GraphTranslationServices { get; set; }

    public async Task<ScanResult> ExecuteScanAsync(IDetectionArguments detectionArguments)
    {
        this.Logger.LogCreateLoggingGroup();
        var initialDetectors = this.DetectorRegistryService.GetDetectors(detectionArguments.AdditionalPluginDirectories, detectionArguments.AdditionalDITargets, detectionArguments.SkipPluginsDirectory).ToImmutableList();

        if (!initialDetectors.Any())
        {
            throw new NoDetectorsFoundException();
        }

        var detectorRestrictions = this.GetDetectorRestrictions(detectionArguments);
        var detectors = this.DetectorRestrictionService.ApplyRestrictions(detectorRestrictions, initialDetectors).ToImmutableList();

        this.Logger.LogVerbose($"Finished applying restrictions to detectors.");

        var processingResult = await this.DetectorProcessingService.ProcessDetectorsAsync(detectionArguments, detectors, detectorRestrictions);

        var graphTranslationService = this.GraphTranslationServices.OrderBy(gts => gts.Metadata.Priority).Last().Value;

        var scanResult = graphTranslationService.GenerateScanResultFromProcessingResult(processingResult, detectionArguments);

        scanResult.DetectorsInScan = detectors.Select(x => ConvertToContract(x)).ToList();
        scanResult.ResultCode = processingResult.ResultCode;

        return scanResult;
    }

    private static Detector ConvertToContract(IComponentDetector detector) => new Detector
    {
        DetectorId = detector.Id,
        IsExperimental = detector is IExperimentalDetector,
        Version = detector.Version,
        SupportedComponentTypes = detector.SupportedComponentTypes,
    };

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
