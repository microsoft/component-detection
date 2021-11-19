using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation
{
    public interface IGraphTranslationService
    {
        ScanResult GenerateScanResultFromProcessingResult(DetectorProcessingResult detectorProcessingResult, IDetectionArguments detectionArguments);
    }
}
