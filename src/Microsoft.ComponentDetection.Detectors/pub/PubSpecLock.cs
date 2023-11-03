namespace Microsoft.ComponentDetection.Detectors.Pub;

using System.Collections.Generic;
using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the pub-spec lock file.
/// </summary>
[DataContract]
public class PubSpecLock
{
    [YamlMember(Alias = "packages")]
    public Dictionary<string, PubSpecLockPackage> Packages { get; set; }

    [YamlMember(Alias = "sdks")]
    public SDK Sdks { get; set; }
}
