#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Legacy data for supporting versions of npm that use lockfileVersion: 1. This is a mapping of package names to
/// dependency objects. Because the object structure is strictly hierarchical, symbolic link dependencies are somewhat
/// challenging to represent in some cases.
/// </summary>
internal sealed record PackageLockV2Dependency
{
    /// <summary>
    /// A specifier that varies depending on the nature of the package, and is usable in fetching a new copy of it.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// A sha512 or sha1 Standard Subresource Integrity string for the artifact that was unpacked in this location. For git
    /// dependencies, this is the commit sha.
    /// </summary>
    [JsonPropertyName("integrity")]
    public string Integrity { get; init; } = null!;

    /// <summary>
    /// For registry sources this is path of the tarball relative to the registry URL. If the tarball URL isn't on the same
    /// server as the registry URL then this is a complete URL. registry.npmjs.org is a magic value meaning "the currently
    /// configured registry".
    /// </summary>
    [JsonPropertyName("resolved")]
    public string Resolved { get; init; } = null!;

    /// <summary>
    /// If true, this is the bundled dependency and will be installed by the parent module. When installing, this module will
    /// be extracted from the parent module during the extract phase, not installed as a separate dependency.
    /// </summary>
    [JsonPropertyName("bundled")]
    public bool Bundled { get; init; }

    /// <summary>
    /// If true then this dependency is either a development dependency ONLY of the top level module or a transitive dependency
    /// of one. This is false for dependencies that are both a development dependency of the top level and a transitive
    /// dependency of a non-development dependency of the top level.
    /// </summary>
    [JsonPropertyName("dev")]
    public bool Dev { get; init; }

    /// <summary>
    /// If true then this dependency is either an optional dependency ONLY of the top level module or a transitive dependency
    /// of one. This is false for dependencies that are both an optional dependency of the top level and a transitive
    /// dependency of a non-optional dependency of the top level.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; init; }

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
    public IDictionary<string, PackageLockV2Dependency>? Dependencies { get; init; }
}
