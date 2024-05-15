namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

public class PnpmHasDependenciesV6
{
    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> Dependencies { get; set; }

    [YamlMember(Alias = "devDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> DevDependencies { get; set; }

    [YamlMember(Alias = "optionalDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> OptionalDependencies { get; set; }
}
