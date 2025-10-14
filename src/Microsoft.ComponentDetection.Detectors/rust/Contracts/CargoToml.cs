#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts;

using System.Runtime.Serialization;

public class CargoToml
{
    [DataMember(Name = "package")]
    public CargoPackage Package { get; set; }
}
