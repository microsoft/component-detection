namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    using System.Diagnostics.CodeAnalysis;

    public class CargoToml
    {
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public CargoPackage package { get; set; }
    }
}
