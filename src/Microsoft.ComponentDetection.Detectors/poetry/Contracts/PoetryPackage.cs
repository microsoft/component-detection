#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
public class PoetryPackage
{
    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "version")]
    public string Version { get; set; }

    [DataMember(Name = "source")]
    public PoetrySource Source { get; set; }

    [DataMember(Name = "dependencies")]
    public Dictionary<string, object> Dependencies { get; set; }

    [DataMember(Name = "extras")]
    public Dictionary<string, object> Extras { get; set; }
}
