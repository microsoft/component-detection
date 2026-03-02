namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents an actor (person, organization, or software agent) involved with a component.
/// At least one of <see cref="Name"/>, <see cref="Email"/>, or <see cref="Url"/> should be populated.
/// Aligned with SPDX 3.0.1 Agent subclasses.
/// </summary>
public class ActorInfo
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Url { get; set; }

    /// <summary>
    /// The type of actor. One of: "Person", "Organization", or "SoftwareAgent".
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }
}
