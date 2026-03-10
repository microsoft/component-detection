namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Represents the author field in a package.json file.
/// </summary>
public sealed record PackageJsonAuthor
{
    /// <summary>
    /// The name of the author.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The email of the author.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// The URL of the author.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
