using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    public class CargoPackage
    {
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string name { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string version { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string author { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string source { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string checksum { get; set; }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public string[] dependencies { get; set; }

        // Get hash code and equals are IDE generated
        // Manually added some casing handling
        public override bool Equals(object obj)
        {
            var package = obj as CargoPackage;
            return package != null &&
                   string.Equals(name, package.name) &&
                   string.Equals(version, package.version, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(source, package.source) &&
                   string.Equals(checksum, package.checksum);
        }

        public override int GetHashCode()
        {
            var hashCode = -2143789899;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(version.ToLowerInvariant());
            return hashCode;
        }
    }
}
