namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SystemTextJson = System.Text.Json.Serialization;

/// <summary>
/// Represents an actor (person, organization, or software agent) involved with a component.
/// At least one of <see cref="Name"/>, <see cref="Email"/>, or <see cref="Url"/> should be populated.
/// Aligned with SPDX 3.0.1 Agent subclasses.
/// </summary>
[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ActorInfo : IEquatable<ActorInfo>
{
    [SystemTextJson.JsonPropertyName("name")]
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [SystemTextJson.JsonPropertyName("email")]
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)]
    public string? Email { get; set; }

    [SystemTextJson.JsonPropertyName("url")]
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? Url { get; set; }

    /// <summary>
    /// The type of actor. One of: "Person", "Organization", or "SoftwareAgent".
    /// </summary>
    [SystemTextJson.JsonPropertyName("type")]
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    /// <inheritdoc/>
    public bool Equals(ActorInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Email, other.Email, StringComparison.OrdinalIgnoreCase)
            && this.Url == other.Url
            && string.Equals(this.Type, other.Type, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => this.Equals(obj as ActorInfo);

    /// <summary>
    /// Must be overridden because <see cref="Equals(ActorInfo)"/> is overridden.
    /// The contract is: if two objects are equal, they must return the same hash code.
    /// Without this override, hash-based collections (HashSet, Dictionary) would use
    /// the default reference-based hash, causing equal-by-value actors to land in
    /// different buckets and fail to deduplicate.
    /// </summary>
    /// <remarks>
    /// Uses the multiply-and-add pattern (seed 17, factor 31) to combine field
    /// hashes into a well-distributed value. <c>unchecked</c> allows intentional integer
    /// overflow without throwing. Each field uses <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// to stay consistent with the case-insensitive equality in <see cref="Equals(ActorInfo)"/>.
    /// </remarks>
    /// <returns>A hash code consistent with value-based equality.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name ?? string.Empty);
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.Email ?? string.Empty);
            hash = (hash * 31) + (this.Url?.GetHashCode() ?? 0);
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.Type ?? string.Empty);
            return hash;
        }
    }
}
