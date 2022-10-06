using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services.GraphTranslation
{
    public interface IGraphTranslationService
    {
        ScanResult GenerateScanResultFromProcessingResult(DetectorProcessingResult detectorProcessingResult, IDetectionArguments detectionArguments);
    }
}
