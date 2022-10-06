using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    public interface IDetectorProcessingService
    {
        Task<DetectorProcessingResult> ProcessDetectorsAsync(IDetectionArguments detectionArguments, IEnumerable<IComponentDetector> detectors, DetectorRestrictions detectorRestrictions);
    }
}
