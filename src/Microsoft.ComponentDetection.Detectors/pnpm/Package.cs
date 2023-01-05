using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Pnpm;
#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
public class Package
{
    public Dictionary<string, string> dependencies { get; set; }

    public string dev { get; set; }

    public string name { get; set; }

    public Dictionary<string, string> resolution { get; set; }

    public string version { get; set; }

    public override string ToString()
    {
        return this.name;
    }
}
#pragma warning restore SA1300
