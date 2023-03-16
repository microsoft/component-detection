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

/// <summary>
/// This is an object that maps package locations to an object containing the information about that package.
/// </summary>
internal sealed record PackageLockV2Package
{
    /// <summary>
    /// The version found in package.json.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// The place where the package was actually resolved from. In the case of packages fetched from the registry, this will be
    /// a url to a tarball. In the case of git dependencies, this will be the full git url with commit sha. In the case of link
    /// dependencies, this will be the location of the link target. registry.npmjs.org is a magic value meaning "the currently
    /// configured registry".
    /// </summary>
    [JsonPropertyName("resolved")]
    public string Resolved { get; init; } = null!;

    /// <summary>
    /// A sha512 or sha1 Standard Subresource Integrity string for the artifact that was unpacked in this location.
    /// </summary>
    [JsonPropertyName("integrity")]
    public string Integrity { get; init; } = null!;

    /// <summary>
    /// A flag to indicate that this is a symbolic link. If this is present, no other fields are specified, since the link
    /// target will also be included in the lockfile.
    /// </summary>
    [JsonPropertyName("link")]
    public bool? Link { get; init; }

    /// <summary>
    /// If the package is strictly part of the devDependencies tree, then dev will be true.
    /// </summary>
    [JsonPropertyName("dev")]
    public bool? Dev { get; init; }

    /// <summary>
    /// If the package is strictly part of the optionalDependencies tree, then optional will be set.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool? Optional { get; init; }

    /// <summary>
    /// If the package  is both a dev dependency and an optional dependency of a non-dev dependency, then devOptional will be
    /// set. (An optional dependency of a dev dependency will have both dev and optional set.)
    /// </summary>
    [JsonPropertyName("devOptional")]
    public bool? DevOptional { get; init; }

    /// <summary>
    /// A flag to indicate that the package is a bundled dependency.
    /// </summary>
    [JsonPropertyName("inBundle")]
    public bool? InBundle { get; init; }

    /// <summary>
    /// A flag to indicate that the package has a preinstall, install, or postinstall script.
    /// </summary>
    [JsonPropertyName("hasInstallScript")]
    public bool? HasInstallScript { get; init; }

    /// <summary>
    /// A flag to indicate that the package has an npm-shrinkwrap.json file.
    /// </summary>
    [JsonPropertyName("hasShrinkwrap")]
    public bool? HasShrinkwrap { get; init; }

    [JsonPropertyName("bin")]
    public IDictionary<string, string>? Bin { get; init; }

    [JsonPropertyName("license")]
    public string? License { get; init; }

    [JsonPropertyName("engines")]
    public IDictionary<string, string>? Engines { get; init; }

    [JsonPropertyName("dependencies")]
    public IDictionary<string, string>? Dependencies { get; init; }

    [JsonPropertyName("optionalDependencies")]
    public IDictionary<string, string>? OptionalDependencies { get; init; }
}

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
