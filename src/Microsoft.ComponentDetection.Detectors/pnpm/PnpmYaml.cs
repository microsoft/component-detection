using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Pnpm
{
#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
    public class PnpmYaml
    {
        public Dictionary<string, string> dependencies { get; set; }

        public Dictionary<string, Package> packages { get; set; }
    }
#pragma warning restore SA1300
}
