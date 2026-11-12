namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerPackage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("require")]
    public Dictionary<string, string?>? Require { get; set; }

    [JsonPropertyName("provide")]
    public Dictionary<string, string?>? Provide { get; set; }

    [JsonPropertyName("replace")]
    public Dictionary<string, string?>? Replace { get; set; }

    [JsonPropertyName("extra")]
    public ComposerExtra? Extra { get; set; }
}
