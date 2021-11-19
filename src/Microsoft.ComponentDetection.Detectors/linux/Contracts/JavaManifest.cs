using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class JavaManifest
    {
        public Dictionary<string, string> Main { get; set; }

        public Dictionary<string, Dictionary<string, string>> NamedSections { get; set; }
    }
}
