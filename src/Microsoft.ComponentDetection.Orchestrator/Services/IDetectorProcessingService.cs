using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator.Services;

public interface IDetectorProcessingService
{
    Task<DetectorProcessingResult> ProcessDetectorsAsync(IDetectionArguments detectionArguments, IEnumerable<IComponentDetector> detectors, DetectorRestrictions detectorRestrictions);
}
