namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a package.json file.
/// https://docs.npmjs.com/cli/v10/configuring-npm/package-json.
/// </summary>
public sealed record PackageJson
{
    /// <summary>
    /// The name of the package.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The version of the package.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// The author of the package. Can be a string or an object with name, email, and url fields.
    /// </summary>
    [JsonPropertyName("author")]
    [JsonConverter(typeof(PackageJsonAuthorConverter))]
    public PackageJsonAuthor? Author { get; init; }

    /// <summary>
    /// If set to true, then npm will refuse to publish it.
    /// </summary>
    [JsonPropertyName("private")]
    public bool? Private { get; init; }

    /// <summary>
    /// The engines that the package is compatible with.
    /// Can be an object mapping engine names to version ranges, or occasionally an array.
    /// </summary>
    [JsonPropertyName("engines")]
    [JsonConverter(typeof(PackageJsonEnginesConverter))]
    public IDictionary<string, string>? Engines { get; init; }

    /// <summary>
    /// Dependencies required to run the package.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IDictionary<string, string>? Dependencies { get; init; }

    /// <summary>
    /// Dependencies only needed for development and testing.
    /// </summary>
    [JsonPropertyName("devDependencies")]
    public IDictionary<string, string>? DevDependencies { get; init; }

    /// <summary>
    /// Dependencies that are optional.
    /// </summary>
    [JsonPropertyName("optionalDependencies")]
    public IDictionary<string, string>? OptionalDependencies { get; init; }

    /// <summary>
    /// Dependencies that will be bundled when publishing the package.
    /// </summary>
    [JsonPropertyName("bundledDependencies")]
    public IList<string>? BundledDependencies { get; init; }

    /// <summary>
    /// Peer dependencies - packages that the consumer must install.
    /// </summary>
    [JsonPropertyName("peerDependencies")]
    public IDictionary<string, string>? PeerDependencies { get; init; }

    /// <summary>
    /// Workspaces configuration. Can be an array of glob patterns or an object with a packages field.
    /// </summary>
    [JsonPropertyName("workspaces")]
    [JsonConverter(typeof(PackageJsonWorkspacesConverter))]
    public IList<string>? Workspaces { get; init; }
}
