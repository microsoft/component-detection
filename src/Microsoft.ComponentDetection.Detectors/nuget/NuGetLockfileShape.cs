namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal record NuGetLockfileShape
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, Dictionary<string, PackageShape>> Dependencies { get; set; } = new();

    public record PackageShape
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("resolved")]
        public string Resolved { get; set; }
    }
}
