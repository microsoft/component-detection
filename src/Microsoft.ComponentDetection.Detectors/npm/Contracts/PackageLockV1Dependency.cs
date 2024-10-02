#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

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
