namespace Microsoft.ComponentDetection.Detectors.Pub;

using System.Runtime.Serialization;
using YamlDotNet.Serialization;

[DataContract]
public class SDK
{
    [YamlMember(Alias = "dart")]
    public string Dart { get; set; }

    [YamlMember(Alias = "flutter")]
    public string Flutter { get; set; }
}
