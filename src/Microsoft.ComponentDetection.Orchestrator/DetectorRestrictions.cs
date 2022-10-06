using System.Collections.Generic;

namespace Microsoft.ComponentDetection.OrchestratorNS
{
    public class DetectorRestrictions
    {
        public IEnumerable<string> AllowedDetectorIds { get; set; }

        public IEnumerable<string> ExplicitlyEnabledDetectorIds { get; set; }

        public IEnumerable<string> AllowedDetectorCategories { get; set; }
    }
}
