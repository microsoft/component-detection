namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;

#pragma warning disable SA1300 // Used for deserialization and the process is case sensitive
public class PnpmHasDependenciesV6
{
    public Dictionary<string, PnpmYamlV6Dependency> dependencies { get; set; }

    public Dictionary<string, PnpmYamlV6Dependency> devDependencies { get; set; }

    public Dictionary<string, PnpmYamlV6Dependency> optionalDependencies { get; set; }
}
#pragma warning restore SA1300
