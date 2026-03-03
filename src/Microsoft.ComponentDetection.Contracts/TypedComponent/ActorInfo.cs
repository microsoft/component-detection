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
public class ActorInfo
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
}
