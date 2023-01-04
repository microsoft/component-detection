using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Orchestrator.Services;

public interface IDetectorRestrictionService
{
    IEnumerable<IComponentDetector> ApplyRestrictions(DetectorRestrictions restrictions, IEnumerable<IComponentDetector> detectors);
}
