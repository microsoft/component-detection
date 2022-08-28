namespace Microsoft.ComponentDetection.Orchestrator
{
    using System.Collections.Generic;

    public class DetectorRestrictions
    {
        public IEnumerable<string> AllowedDetectorIds { get; set; }

        public IEnumerable<string> ExplicitlyEnabledDetectorIds { get; set; }

        public IEnumerable<string> AllowedDetectorCategories { get; set; }
    }
}
