using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts;

public class CargoToml
{
    [DataMember(Name = "package")]
    public CargoPackage Package { get; set; }
}
