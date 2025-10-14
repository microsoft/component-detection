#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;

public class DetectorRestrictions
{
    public IEnumerable<string> AllowedDetectorIds { get; set; }

    public IEnumerable<string> ExplicitlyEnabledDetectorIds { get; set; }

    public IEnumerable<string> AllowedDetectorCategories { get; set; }

    public IEnumerable<IComponentDetector> DisabledDetectors { get; set; }
}
