namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the metadata from a Syft scan source of type "image".
/// Contains image details such as layers, labels, tags, and image ID.
/// Deserialized from the <c>source.metadata</c> field in Syft JSON output,
/// which is typed as <c>object</c> in the auto-generated <see cref="SourceClass"/>.
/// </summary>
internal class SyftSourceMetadata
{
    [JsonPropertyName("userInput")]
    public string? UserInput { get; set; }

    [JsonPropertyName("imageID")]
    public string? ImageId { get; set; }

    [JsonPropertyName("manifestDigest")]
    public string? ManifestDigest { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("imageSize")]
    public long? ImageSize { get; set; }

    [JsonPropertyName("layers")]
    public SyftSourceLayer[]? Layers { get; set; }

    [JsonPropertyName("repoDigests")]
    public string[]? RepoDigests { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }
}
