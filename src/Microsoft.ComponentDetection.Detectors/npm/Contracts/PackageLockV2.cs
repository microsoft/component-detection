#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// https://docs.npmjs.com/cli/v8/configuring-npm/package-lock-json.
/// </summary>
internal sealed record PackageLockV2
{
    /// <summary>
    /// The name of the package this is a package-lock for. This will match what's in package.json.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// The version of the package this is a package-lock for. This will match what's in package.json.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// An integer version, starting at 1 with the version number of this document whose semantics were used when generating
    /// this package-lock.json.
    /// </summary>
    [JsonPropertyName("lockfileVersion")]
    public uint LockfileVersion { get; init; } = 2;

    /// <summary>
    /// This is an object that maps package locations to an object containing the information about that package.
    /// The root project is typically listed with a key of "", and all other packages are listed with their relative paths from
    /// the root project folder.
    /// </summary>
    [JsonPropertyName("packages")]
    public IDictionary<string, PackageLockV2Package>? Packages { get; init; }

    /// <summary>
    /// Legacy data for supporting versions of npm that use lockfileVersion: 1. This is a mapping of package names to
    /// dependency objects. Because the object structure is strictly hierarchical, symbolic link dependencies are somewhat
    /// challenging to represent in some cases.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IDictionary<string, PackageLockV2Dependency>? Dependencies { get; init; }
}
