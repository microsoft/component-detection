#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

public class CargoPackage
{
    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "version")]
    public string Version { get; set; }

    [DataMember(Name = "author")]
    public string Author { get; set; }

    [DataMember(Name = "source")]
    public string Source { get; set; }

    [DataMember(Name = "checksum")]
    public string Checksum { get; set; }

    [DataMember(Name = "dependencies")]
    public string[] Dependencies { get; set; }

    // Get hash code and equals are IDE generated
    // Manually added some casing handling
    public override bool Equals(object obj)
    {
        return obj is CargoPackage package &&
               string.Equals(this.Name, package.Name) &&
               string.Equals(this.Version, package.Version, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(this.Source, package.Source) &&
               string.Equals(this.Checksum, package.Checksum);
    }

    [SuppressMessage("Usage", "CA1308:Normalize String to Uppercase", Justification = "Casing cannot be overwritten.")]
    public override int GetHashCode()
    {
        return HashCode.Combine(
            EqualityComparer<string>.Default.GetHashCode(this.Name),
            EqualityComparer<string>.Default.GetHashCode(this.Version.ToLowerInvariant()),
            EqualityComparer<string>.Default.GetHashCode(this.Source),
            EqualityComparer<string>.Default.GetHashCode(this.Checksum));
    }
}
