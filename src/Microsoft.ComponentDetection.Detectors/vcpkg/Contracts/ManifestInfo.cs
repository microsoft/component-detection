#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System.Text.Json.Serialization;

public class ManifestInfo
{
    [JsonPropertyName("manifest-path")]
    public string ManifestPath { get; set; }
}
