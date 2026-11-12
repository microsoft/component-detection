namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Text.Json.Serialization;

internal class ComposerPlatformPackage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("direct-dependency")]
    public bool? DirectDependency { get; set; }
}
