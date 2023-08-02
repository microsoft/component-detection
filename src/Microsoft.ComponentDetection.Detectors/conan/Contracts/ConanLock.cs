namespace Microsoft.ComponentDetection.Detectors.Conan.Contracts;

using System;
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

    public override bool Equals(object obj) => obj is ConanLock conanLock && this.Version == conanLock.Version && this.ProfileHost == conanLock.ProfileHost && this.ProfileBuild == conanLock.ProfileBuild && this.GraphLock.Equals(conanLock.GraphLock);

    public override int GetHashCode() => HashCode.Combine(this.Version, this.ProfileHost, this.ProfileBuild, this.GraphLock);

    internal bool HasNodes() => this.GraphLock?.Nodes?.Count > 0;
}
