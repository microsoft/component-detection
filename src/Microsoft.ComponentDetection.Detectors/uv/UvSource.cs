namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Text.Json.Serialization;

internal class UvSource
{
    [JsonPropertyName("registry")]
    public string? Registry { get; set; }

    [JsonPropertyName("virtual")]
    public string? Virtual { get; set; }

    [JsonPropertyName("git")]
    public string? Git { get; set; }
}
