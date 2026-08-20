#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Text.Json.Serialization;

/// <summary>
/// Metadata for a pip component being installed. See https://packaging.python.org/en/latest/specifications/core-metadata/.
/// Some fields are not collected here because they are not needed for dependency graph construction.
/// </summary>
public sealed record PipInstallationMetadata
{
    /// <summary>
    /// Version of the file format; legal values are "1.0", "1.1", "1.2", "2.1", "2.2", and "2.3"
    /// as of May 2024.
    /// </summary>
    [JsonPropertyName("metadata_version")]
    public string MetadataVersion { get; set; }

    /// <summary>
    /// The name of the distribution.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// A string containing the distribution's version number.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; }

    /// <summary>
    /// Each entry contains a string naming some other distutils project required by this distribution.
    /// See https://peps.python.org/pep-0508/ for the format of the strings.
    /// </summary>
    [JsonPropertyName("requires_dist")]
    public string[] RequiresDist { get; set; }

    /// <summary>
    /// URL for the distribution's home page.
    /// </summary>
    [JsonPropertyName("home_page")]
    public string HomePage { get; set; }

    /// <summary>
    /// Maintainer's name at a minimum; additional contact information may be provided.
    /// </summary>
    [JsonPropertyName("maintainer")]
    public string Maintainer { get; set; }

    /// <summary>
    /// Maintainer’s e-mail address. It can contain a name and e-mail address in the legal forms for a RFC-822 From: header.
    /// </summary>
    [JsonPropertyName("maintainer_email")]
    public string MaintainerEmail { get; set; }

    /// <summary>
    /// Author's name at a minimum; additional contact information may be provided.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; }

    /// <summary>
    /// Author's e-mail address. It can contain a name and e-mail address in the legal forms for a RFC-822 From: header.
    /// </summary>
    [JsonPropertyName("author_email")]
    public string AuthorEmail { get; set; }

    /// <summary>
    /// Text indicating the license covering the distribution.
    /// </summary>
    [JsonPropertyName("license")]
    public string License { get; set; }

    /// <summary>
    /// Each entry is a string giving a single classification value for the distribution.
    /// Classifiers are described in PEP 301 https://peps.python.org/pep-0301/.
    /// </summary>
    [JsonPropertyName("classifier")]
    public string[] Classifier { get; set; }
}
