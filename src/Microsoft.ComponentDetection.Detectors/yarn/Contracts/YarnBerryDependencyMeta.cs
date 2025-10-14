#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using YamlDotNet.Serialization;

/// <summary>
/// Represents the metadata of a dependency in the yarn.lock file for yarn v2+ (berry).
/// </summary>
public sealed record YarnBerryDependencyMeta
{
    /// <summary>
    /// Whether the dependency is pre-built.
    /// </summary>
    [YamlMember(Alias = "built")]
    public bool? Built { get; init; }

    /// <summary>
    /// Whether the dependency is optional.
    /// </summary>
    [YamlMember(Alias = "optional")]
    public bool? Optional { get; init; }

    /// <summary>
    /// Whether the dependency is unplugged.
    /// This means that the dependency is not referenced directly through it's archive.
    /// Instead, it will be unpacked at install time into the `pnpUnplugged` folder.
    /// </summary>
    [YamlMember(Alias = "unplugged")]
    public bool? Unplugged { get; init; }
}
