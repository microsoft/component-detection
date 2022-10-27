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
            return obj is CargoPackage package &&
                   string.Equals(this.name, package.name) &&
                   string.Equals(this.version, package.version, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(this.source, package.source) &&
                   string.Equals(this.checksum, package.checksum);
        }

        public override int GetHashCode() => HashCode.Combine(
                EqualityComparer<string>.Default.GetHashCode(this.name),
                #pragma warning disable CA1308
                EqualityComparer<string>.Default.GetHashCode(this.version.ToLowerInvariant()),
                #pragma warning restore CA1308
                EqualityComparer<string>.Default.GetHashCode(this.source),
                EqualityComparer<string>.Default.GetHashCode(this.checksum));
    }
}
