#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts;

using System.Runtime.Serialization;

internal class CargoToml
{
    [DataMember(Name = "package")]
    public CargoPackage Package { get; set; }
}
