#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Format for a Pnpm lock file version 5 as defined in https://github.com/pnpm/spec/blob/master/lockfile/5.md.
/// </summary>
public class PnpmYamlV5 : PnpmYaml
{
    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, string> Dependencies { get; set; }

    [YamlMember(Alias = "packages")]
    public Dictionary<string, Package> Packages { get; set; }
}
