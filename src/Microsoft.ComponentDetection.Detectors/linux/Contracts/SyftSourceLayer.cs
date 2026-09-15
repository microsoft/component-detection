namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a single layer in the image source metadata from Syft output.
/// </summary>
internal class SyftSourceLayer
{
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}
