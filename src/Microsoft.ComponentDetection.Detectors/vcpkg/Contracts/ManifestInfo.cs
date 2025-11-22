#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System.Text.Json.Serialization;
using Newtonsoft.Json;

public class ManifestInfo
{
    [JsonProperty("manifest-path")]
    [JsonPropertyName("manifest-path")]
    public string ManifestPath { get; set; }
}
