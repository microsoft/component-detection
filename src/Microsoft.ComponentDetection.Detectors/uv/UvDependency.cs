namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Text.Json.Serialization;

internal class UvDependency
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("specifier")]
    public string? Specifier { get; set; }
}
