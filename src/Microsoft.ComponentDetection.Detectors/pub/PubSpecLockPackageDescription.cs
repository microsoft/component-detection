namespace Microsoft.ComponentDetection.Detectors.Pub;

using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the pub-spec lock file.
/// </summary>
[DataContract]
public class PubSpecLockPackageDescription
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; }

    [YamlMember(Alias = "sha256")]
    public string Sha256 { get; set; }

    [YamlMember(Alias = "url")]
    public string Url { get; set; }
}
