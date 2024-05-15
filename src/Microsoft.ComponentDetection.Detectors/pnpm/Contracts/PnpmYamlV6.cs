namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;

#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
/// <summary>
/// Format for a Pnpm lock file version 6 as defined in https://github.com/pnpm/spec/blob/master/lockfile/6.0.md.
///
/// This handles both the "dedicated shrinkwrap" and "shared shrinkwrap" usages.
/// In the "dedicated shrinkwrap", the inherited members from PnpmHasDependenciesV6 will be used.
/// In the "shared shrinkwrap", the importers member will be used.
/// </summary>
public class PnpmYamlV6 : PnpmHasDependenciesV6
{
    public Dictionary<string, PnpmHasDependenciesV6> importers { get; set; }

    public Dictionary<string, Package> packages { get; set; }

    public string lockfileVersion { get; set; }
}
#pragma warning restore SA1300
