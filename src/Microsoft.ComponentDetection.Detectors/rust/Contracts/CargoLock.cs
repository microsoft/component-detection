#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;

// Represents Cargo.Lock file structure.
[DataContract]
public class CargoLock
{
    [DataMember(Name = "package")]
    public List<CargoPackage> Package { get; set; }

    [DataMember(Name = "metadata")]
    public Dictionary<string, object> Metadata { get; set; }

    [DataMember(Name = "version")]
    public int Version { get; set; }
}
