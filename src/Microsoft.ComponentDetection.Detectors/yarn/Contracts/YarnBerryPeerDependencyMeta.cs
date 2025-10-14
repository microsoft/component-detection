#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using YamlDotNet.Serialization;

/// <summary>
/// Represents the metadata of a peer dependency in the yarn.lock file for yarn v2+ (berry).
/// </summary>
public sealed record YarnBerryPeerDependencyMeta
{
    /// <summary>
    /// Whether the dependency is optional.
    /// </summary>
    [YamlMember(Alias = "optional")]
    public bool Optional { get; init; }
}
