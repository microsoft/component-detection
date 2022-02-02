using System.Diagnostics.CodeAnalysis;

namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts
{
    // Represents Poetry.Lock file structure.
    public class PoetryLock
    {
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Deserialization contract. Casing cannot be overwritten.")]
        public PoetryPackage[] package { get; set; }
    }
}
