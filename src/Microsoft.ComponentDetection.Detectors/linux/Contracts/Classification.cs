using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Classification
    {
        public string Class { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
    }
}
