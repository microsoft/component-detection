#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// There is still no official docs for the new v9 lock if format, so these parsing contracts are empirically based.
/// Issue tracking v9 specs: https://github.com/pnpm/spec/issues/6
/// Format should eventually get updated here: https://github.com/pnpm/spec/blob/master/lockfile/6.0.md.
/// </summary>
public class PnpmYamlV9 : PnpmHasDependenciesV9
{
    [YamlMember(Alias = "importers")]
    public Dictionary<string, PnpmHasDependenciesV9> Importers { get; set; }

    [YamlMember(Alias = "packages")]
    public Dictionary<string, Package> Packages { get; set; }

    [YamlMember(Alias = "snapshots")]
    public Dictionary<string, Package> Snapshots { get; set; }
}
