#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using YamlDotNet.Serialization;

/// <summary>
/// Represents the metadata of the yarn.lock file for yarn v2+.
/// </summary>
public sealed record YarnBerryLockfileMetadata
{
    /// <summary>
    /// Gets the version of the yarn.lock file.
    /// </summary>
    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    /// <summary>
    /// Gets the cache key of the yarn.lock file.
    /// </summary>
    [YamlMember(Alias = "cacheKey")]
    public string CacheKey { get; set; }
}
