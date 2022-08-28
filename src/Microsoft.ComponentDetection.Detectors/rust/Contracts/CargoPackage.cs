namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

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
            return package != null && this.name.Equals(package.name) && this.version.Equals(package.version, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = -2143789899;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.name);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.version.ToLowerInvariant());
            return hashCode;
        }
    }
}
