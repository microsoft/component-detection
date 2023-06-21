#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// This is an object that maps package locations to an object containing the information about that package.
/// </summary>
internal sealed record PackageLockV3Package
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
