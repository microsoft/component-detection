#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// https://docs.npmjs.com/cli/v6/configuring-npm/package-lock-json.
/// </summary>
internal sealed record PackageLockV1
{
    /// <summary>
    /// The name of the package this is a package-lock for. This must match what's in package.json.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// The version of the package this is a package-lock for. This must match what's in package.json.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// An integer version, starting at 1 with the version number of this document whose semantics were used when generating
    /// this package-lock.json.
    /// </summary>
    [JsonPropertyName("lockfileVersion")]
    public uint LockfileVersion { get; init; } = 1;

    /// <summary>
    /// This is a subresource integrity value created from the package.json. No preprocessing of the package.json should be
    /// done. Subresource integrity strings can be produced by modules like ssri.
    /// </summary>
    [JsonPropertyName("packageIntegrity")]
    public string? PackageIntegrity { get; init; }

    /// <summary>
    /// Indicates that the install was done with the environment variable NODE_PRESERVE_SYMLINKS enabled. The installer should
    /// insist that the value of this property match that environment variable.
    /// </summary>
    [JsonPropertyName("preserveSymlinks")]
    public string? PreserveSymlinks { get; init; }

    [JsonPropertyName("requires")]
    public bool Requires { get; init; }

    /// <summary>
    /// A mapping of package name to dependency object.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IDictionary<string, PackageLockV1Dependency>? Dependencies { get; init; }
}
