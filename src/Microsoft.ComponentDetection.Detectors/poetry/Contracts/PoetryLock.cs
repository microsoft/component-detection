#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;

// Represents Poetry.Lock file structure.
[DataContract]
public class PoetryLock
{
    [DataMember(Name = "Package")]
    public List<PoetryPackage> Package { get; set; }

    [DataMember(Name = "metadata")]
    public Dictionary<string, object> Metadata { get; set; }
}
