#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Conan.Contracts;

using System.Text.Json.Serialization;

public class ConanLock
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("profile_host")]
    public string ProfileHost { get; set; }

    [JsonPropertyName("profile_build")]
    public string ProfileBuild { get; set; }

    [JsonPropertyName("graph_lock")]
    public ConanLockGraph GraphLock { get; set; }

    internal bool HasNodes() => this.GraphLock?.Nodes?.Count > 0;
}
