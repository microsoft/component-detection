#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

internal class PnpmHasDependenciesV6 : PnpmYaml
{
    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> Dependencies { get; set; }

    [YamlMember(Alias = "devDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> DevDependencies { get; set; }

    [YamlMember(Alias = "optionalDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> OptionalDependencies { get; set; }

    [YamlMember(Alias = "packageManagerDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> PackageManagerDependencies { get; set; }

    [YamlMember(Alias = "configDependencies")]
    public Dictionary<string, PnpmYamlV6Dependency> ConfigDependencies { get; set; }
}
