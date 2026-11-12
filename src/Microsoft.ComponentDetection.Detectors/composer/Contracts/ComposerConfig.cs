namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Text.Json.Serialization;

internal class ComposerConfig
{
    [JsonPropertyName("vendor-dir")]
    public string? VendorDirectory { get; set; }
}
