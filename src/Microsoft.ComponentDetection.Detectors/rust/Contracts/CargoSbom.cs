#nullable disable

// Schema for Cargo SBOM pre-cursor files (*.cargo-sbom.json)
namespace Microsoft.ComponentDetection.Detectors.Rust.Sbom.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable SA1402
#pragma warning disable SA1204

/// <summary>
/// Type of dependency.
/// </summary>
public enum SbomKind
{
    /// <summary>
    /// A dependency linked to the artifact produced by this crate.
    /// </summary>
    Normal,

    /// <summary>
    /// A compile-time dependency used to build this crate.
    /// </summary>
    Build,

    /// <summary>
    /// An unexpected dependency kind.
    /// </summary>
    Unknown,
}

/// <summary>
/// Represents the Cargo Software Bill of Materials (SBOM).
/// </summary>
public class CargoSbom
{
    /// <summary>
    /// Gets or sets the version of the SBOM.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the index of the root crate.
    /// </summary>
    public int Root { get; set; }

    /// <summary>
    /// Gets or sets the list of crates.
    /// </summary>
    public List<SbomCrate> Crates { get; set; }

    /// <summary>
    /// Gets or sets the information about rustc used to perform the compilation.
    /// </summary>
    public Rustc Rustc { get; set; }

    /// <summary>
    /// Deserialize from JSON.
    /// </summary>
    /// <returns>Cargo SBOM.</returns>
    public static CargoSbom FromJson(string json) => JsonSerializer.Deserialize<CargoSbom>(json, Converter.Settings);
}

/// <summary>
/// Represents a crate in the SBOM.
/// </summary>
public class SbomCrate
{
    /// <summary>
    /// Gets or sets the Cargo Package ID specification.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the enabled feature flags.
    /// </summary>
    public List<string> Features { get; set; }

    /// <summary>
    /// Gets or sets the enabled cfg attributes set by build scripts.
    /// </summary>
    public List<string> Cfgs { get; set; }

    /// <summary>
    /// Gets or sets the dependencies for this crate.
    /// </summary>
    public List<SbomDependency> Dependencies { get; set; }
}

/// <summary>
/// Represents a dependency of a crate.
/// </summary>
public class SbomDependency
{
    /// <summary>
    /// Gets or sets the index into the crates array.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the kind of dependency.
    /// </summary>
    public SbomKind Kind { get; set; }
}

/// <summary>
/// Represents information about rustc used to perform the compilation.
/// </summary>
public class Rustc
{
    /// <summary>
    /// Gets or sets the compiler version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the compiler wrapper.
    /// </summary>
    public string Wrapper { get; set; }

    /// <summary>
    /// Gets or sets the compiler workspace wrapper.
    /// </summary>
    public string WorkspaceWrapper { get; set; }

    /// <summary>
    /// Gets or sets the commit hash for rustc.
    /// </summary>
    public string CommitHash { get; set; }

    /// <summary>
    /// Gets or sets the host target triple.
    /// </summary>
    [JsonPropertyName("host")]
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the verbose version string.
    /// </summary>
    public string VerboseVersion { get; set; }
}

/// <summary>
/// Deserializes SbomKind.
/// </summary>
internal class SbomKindConverter : JsonConverter<SbomKind>
{
    public static readonly SbomKindConverter Singleton = new SbomKindConverter();

    public override bool CanConvert(Type t) => t == typeof(SbomKind);

    public override SbomKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "build" => SbomKind.Build,
            "normal" => SbomKind.Normal,
            _ => SbomKind.Unknown,
        };
    }

    public override void Write(Utf8JsonWriter writer, SbomKind value, JsonSerializerOptions options) => throw new NotImplementedException();
}

/// <summary>
/// Json converter settings.
/// </summary>
internal static class Converter
{
    public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
    {
        Converters = { SbomKindConverter.Singleton },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
