namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Text.Json.Serialization;

internal class ComposerAlias
{
    [JsonPropertyName("package")]
    public string? Package { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }
}
