namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;

using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public interface IGraphTranslationService
{
    ScanResult GenerateScanResultFromProcessingResult(DetectorProcessingResult detectorProcessingResult, IDetectionArguments detectionArguments, bool updateLocations = true);
}
