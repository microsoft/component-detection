namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerManifest : ComposerPackage
{
    [JsonPropertyName("require-dev")]
    public Dictionary<string, string?>? DevelopmentRequirements { get; set; }

    [JsonPropertyName("config")]
    public ComposerConfig? Config { get; set; }
}
