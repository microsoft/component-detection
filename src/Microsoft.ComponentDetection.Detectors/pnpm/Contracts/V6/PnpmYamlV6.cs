#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Format for a Pnpm lock file version 6 as defined in https://github.com/pnpm/spec/blob/master/lockfile/6.0.md.
///
/// This handles both the "dedicated shrinkwrap" and "shared shrinkwrap" usages.
/// In the "dedicated shrinkwrap", the inherited members from PnpmHasDependenciesV6 will be used.
/// In the "shared shrinkwrap", the importers member will be used.
/// </summary>
public class PnpmYamlV6 : PnpmHasDependenciesV6
{
    [YamlMember(Alias = "importers")]
    public Dictionary<string, PnpmHasDependenciesV6> Importers { get; set; }

    [YamlMember(Alias = "packages")]
    public Dictionary<string, Package> Packages { get; set; }
}
