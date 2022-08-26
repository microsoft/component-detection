using System.Diagnostics.CodeAnalysis;

namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    using System.Collections.Generic;
    using Tomlyn.Model;

    // Represents Cargo.Lock file structure.
    public class CargoLock
    {
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public List<CargoPackage> package { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public TomlTable metadata { get; set; }
    }
}
