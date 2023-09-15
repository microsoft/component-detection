namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Commands;

public interface IDetectorProcessingService
{
    Task<DetectorProcessingResult> ProcessDetectorsAsync(
        ScanSettings settings,
        IEnumerable<IComponentDetector> detectors,
        DetectorRestrictions detectorRestrictions);
}
