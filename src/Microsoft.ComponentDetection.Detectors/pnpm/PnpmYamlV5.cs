namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;

#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
public class PnpmYamlV5
{
    public Dictionary<string, Package> packages { get; set; }

    public string lockfileVersion { get; set; }
}
#pragma warning restore SA1300
