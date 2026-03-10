#nullable disable
namespace Microsoft.ComponentDetection.Detectors.CondaLock.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the package section in the conda-lock file.
/// </summary>
[DataContract]
public class CondaPackage
{
    [YamlMember(Alias = "category")]
    public string Category { get; set; }

    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, string> Dependencies { get; set; }

    [YamlMember(Alias = "hash")]
    public Dictionary<string, string> Hash { get; set; }

    [YamlMember(Alias = "manager")]
    public string Manager { get; set; }

    [YamlMember(Alias = "name")]
    public string Name { get; set; }

    [YamlMember(Alias = "optional")]
    public bool Optional { get; set; }

    [YamlMember(Alias = "platform")]
    public string Platform { get; set; }

    [YamlMember(Alias = "url")]
    public string Url { get; set; }

    [YamlMember(Alias = "version")]
    public string Version { get; set; }
}
