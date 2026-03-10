#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using YamlDotNet.Serialization;

public class Package
{
    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, string> Dependencies { get; set; }

    [YamlMember(Alias = "dev")]
    public string Dev { get; set; }

    [YamlMember(Alias = "name")]
    public string Name { get; set; }

    [YamlMember(Alias = "resolution")]
    public Dictionary<string, string> Resolution { get; set; }

    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    public override string ToString()
    {
        return this.Name;
    }
}
