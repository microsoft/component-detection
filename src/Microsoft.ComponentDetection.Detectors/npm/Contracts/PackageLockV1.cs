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

internal sealed record PackageLockV1Dependency
{
    /// <summary>
    /// This is a specifier that uniquely identifies this package and should be usable in fetching a new copy of it.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    ///     <ul>
    ///         <li>For bundled dependencies this is not included, regardless of source.</li>
    ///         <li>
    ///         For registry sources this is path of the tarball relative to the registry URL. If the tarball URL isn't on
    ///         the same server as the registry URL then this is a complete URL.
    ///         </li>
    ///     </ul>
    /// </summary>
    [JsonPropertyName("resolved")]
    public string Resolved { get; init; } = null!;

    /// <summary>
    /// This is a Standard Subresource Integrity for this resource.
    /// <ul>
    ///     <li>For bundled dependencies this is not included, regardless of source.</li>
    ///     <li>
    ///     For registry sources, this is the integrity that the registry provided, or if one wasn't provided the SHA1 in
    ///     shasum.
    ///     </li>
    ///     <li> For git sources this is the specific commit hash we cloned from.</li>
    ///     <li> For remote tarball sources this is an integrity based on a SHA512 of the file.</li>
    ///     <li> For local tarball sources: This is an integrity field based on the SHA512 of the file.</li>
    /// </ul>
    /// </summary>
    [JsonPropertyName("integrity")]
    public string Integrity { get; init; } = null!;

    /// <summary>
    /// If true, this is the bundled dependency and will be installed by the parent module. When installing, this module will
    /// be extracted from the parent module during the extract phase, not installed as a separate dependency.
    /// </summary>
    [JsonPropertyName("bundled")]
    public bool? Bundled { get; init; }

    /// <summary>
    /// If true then this dependency is either a development dependency ONLY of the top level module or a transitive dependency
    /// of one. This is false for dependencies that are both a development dependency of the top level and a transitive
    /// dependency of a non-development dependency of the top level.
    /// </summary>
    [JsonPropertyName("dev")]
    public bool? Dev { get; init; }

    /// <summary>
    /// If true then this dependency is either an optional dependency ONLY of the top level module or a transitive dependency
    /// of one. This is false for dependencies that are both an optional dependency of the top level and a transitive
    /// dependency of a non-optional dependency of the top level.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool? Optional { get; init; }

    /// <summary>
    /// This is a mapping of module name to version. This is a list of everything this module requires, regardless of where it
    /// will be installed. The version should match via normal matching rules a dependency either in our dependencies or in a
    /// level higher than us.
    /// </summary>
    [JsonPropertyName("requires")]
    public IDictionary<string, string>? Requires { get; init; }

    /// <summary>
    /// The dependencies of this dependency, exactly as at the top level.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IDictionary<string, PackageLockV1Dependency>? Dependencies { get; init; }
}
