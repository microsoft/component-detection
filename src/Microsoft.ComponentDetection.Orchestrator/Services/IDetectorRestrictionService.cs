#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;

public interface IDetectorRestrictionService
{
    IEnumerable<IComponentDetector> ApplyRestrictions(DetectorRestrictions restrictions, IEnumerable<IComponentDetector> detectors);
}
