#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Represents an entry in the yarn.lock file for yarn v2+ (berry).
/// There is no official documentation for the format of the yarn.lock file.
/// </summary>
public sealed record YarnBerryLockfileEntry
{
    /// <summary>
    /// The version of the package, if available.
    /// </summary>
    [YamlMember(Alias = "version")]
    public string? Version { get; init; }

    /// <summary>
    /// Package currently used to resolve the dependency. May be null if the
    /// dependency couldn't be resolved. This can happen in those cases:
    /// <ul>
    ///     <li>
    ///     The dependency is a peer dependency; workspaces don't have ancestors to satisfy their peer dependencies, so they're
    ///     always unresolved. This is why we recommend to list a dev dependency for each non-optional peer
    ///     dependency you list, so that Yarn can fallback to it.
    ///     </li>
    ///     <li>
    ///     The dependency is a prod dependency, and there's a dev dependency of the same name (in which case there will be a
    ///     separate dependency entry for the dev dependency, which will have the resolution).
    ///     </li>
    /// </ul>.
    /// </summary>
    [YamlMember(Alias = "resolution")]
    public string? Resolution { get; init; }

    /// <summary>
    /// A map of the package's dependencies. There's no distinction between prod dependencies and dev dependencies, because
    /// those have already been merged together during the resolution process.
    /// </summary>
    [YamlMember(Alias = "dependencies")]
    public IDictionary<string, string> Dependencies { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// A map of the package's peer dependencies.
    /// </summary>
    [YamlMember(Alias = "peerDependencies")]
    public IDictionary<string, string> PeerDependencies { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Map with additional information about direct dependencies.
    /// </summary>
    [YamlMember(Alias = "dependenciesMeta")]
    public IDictionary<string, YarnBerryDependencyMeta> DependenciesMeta { get; init; } =
        new Dictionary<string, YarnBerryDependencyMeta>();

    /// <summary>
    /// Map with additional information about peer dependencies.
    /// The keys are stringified idents, for example: `@scope/name`.
    /// </summary>
    [YamlMember(Alias = "peerDependenciesMeta")]
    public IDictionary<string, YarnBerryPeerDependencyMeta> PeerDependenciesMeta { get; init; } =
        new Dictionary<string, YarnBerryPeerDependencyMeta>();

    /// <summary>
    /// All `bin` entries defined by the package.
    /// </summary>
    [YamlMember(Alias = "bin")]
    public IDictionary<string, string>? Bin { get; init; }

    /// <summary>
    /// Describes the type of the file system link for a package.
    /// </summary>
    [YamlMember(Alias = "linkType")]
    public string? LinkType { get; init; }

    /// <summary>
    /// The "language" of the package (eg. `node`), for use with multi-linkers.
    /// </summary>
    [YamlMember(Alias = "languageName")]
    public string? LanguageName { get; init; }

    /// <summary>
    /// The checksum (SHA-512 hash) of the package.
    /// </summary>
    [YamlMember(Alias = "checksum")]
    public string Checksum { get; init; } = string.Empty;

    /// <summary>
    /// A set of constraints indicating whether the package supports the host environment.
    /// </summary>
    [YamlMember(Alias = "conditions")]
    public string? Conditions { get; init; }
}
