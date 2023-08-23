namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public interface IDetectorProcessingService
{
    Task<DetectorProcessingResult> ProcessDetectorsAsync(IDetectionArguments detectionArguments, IEnumerable<IComponentDetector> detectors, DetectorRestrictions detectorRestrictions);
}
