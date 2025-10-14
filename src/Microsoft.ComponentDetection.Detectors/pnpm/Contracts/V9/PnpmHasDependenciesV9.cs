#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

public class PnpmHasDependenciesV9 : PnpmYaml
{
    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, PnpmYamlV9Dependency> Dependencies { get; set; }

    [YamlMember(Alias = "devDependencies")]
    public Dictionary<string, PnpmYamlV9Dependency> DevDependencies { get; set; }

    [YamlMember(Alias = "optionalDependencies")]
    public Dictionary<string, PnpmYamlV9Dependency> OptionalDependencies { get; set; }
}
