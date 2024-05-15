namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;

#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
/// <summary>
/// Format for a Pnpm lock file version 5 as defined in https://github.com/pnpm/spec/blob/master/lockfile/5.md.
/// </summary>
public class PnpmYamlV5
{
    public Dictionary<string, string> dependencies { get; set; }

    public Dictionary<string, Package> packages { get; set; }

    public string lockfileVersion { get; set; }
}
#pragma warning restore SA1300
