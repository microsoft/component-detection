#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using Newtonsoft.Json;

public class ManifestInfo
{
    [JsonProperty("manifest-path")]
    public string ManifestPath { get; set; }
}
