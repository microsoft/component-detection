#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Ivy;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the root object of the Ivy usage file.
/// </summary>
internal sealed record IvyUsage
{
    /// <summary>
    /// The list of usage entries.
    /// </summary>
    [JsonPropertyName("RegisterUsage")]
    public IEnumerable<IvyUsageEntry> RegisterUsage { get; init; } = null!;
}

/// <summary>
/// Represents a usage entry in Ivy.
/// </summary>
internal sealed record IvyUsageEntry
{
    /// <summary>
    /// The GAV of the dependency.
    /// </summary>
    [JsonPropertyName("gav")]
    public IvyGav Gav { get; init; } = null!;

    /// <summary>
    /// Indicates whether the dependency is a development dependency.
    /// </summary>
    [JsonPropertyName("DevelopmentDependency")]
    public bool IsDevelopmentDependency { get; init; }

    /// <summary>
    /// Indicates whether the dependency is resolved.
    /// </summary>
    [JsonPropertyName("resolved")]
    public bool IsResolved { get; init; }

    /// <summary>
    /// The parent GAV of the dependency.
    /// </summary>
    [JsonPropertyName("parent_gav")]
    public IvyGav? ParentGav { get; init; }
}

/// <summary>
/// Represents a GAV (GroupId, ArtifactId, Version) in Ivy.
/// </summary>
internal sealed record IvyGav
{
    /// <summary>
    /// The group ID of the dependency.
    /// </summary>
    [JsonPropertyName("g")]
    public string GroupId { get; init; } = null!;

    /// <summary>
    /// The artifact ID of the dependency.
    /// </summary>
    [JsonPropertyName("a")]
    public string ArtifactId { get; init; } = null!;

    /// <summary>
    /// The version of the dependency.
    /// </summary>
    [JsonPropertyName("v")]
    public string Version { get; init; } = null!;
}
