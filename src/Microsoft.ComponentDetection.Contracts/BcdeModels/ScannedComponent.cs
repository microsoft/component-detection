#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ScannedComponent
{
    [JsonPropertyName("locationsFoundAt")]
    public IEnumerable<string> LocationsFoundAt { get; set; }

    [JsonPropertyName("component")]
    public TypedComponent.TypedComponent Component { get; set; }

    [JsonPropertyName("detectorId")]
    public string DetectorId { get; set; }

    [JsonPropertyName("isDevelopmentDependency")]
    public bool? IsDevelopmentDependency { get; set; }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [JsonPropertyName("dependencyScope")]
    public DependencyScope? DependencyScope { get; set; }

    [JsonPropertyName("topLevelReferrers")]
    public IEnumerable<TypedComponent.TypedComponent> TopLevelReferrers { get; set; }

    [JsonPropertyName("ancestralReferrers")]
    public IEnumerable<TypedComponent.TypedComponent> AncestralReferrers { get; set; }

    [JsonPropertyName("containerDetailIds")]
    public IEnumerable<int> ContainerDetailIds { get; set; }

    [JsonPropertyName("containerLayerIds")]
    public IDictionary<int, IEnumerable<int>> ContainerLayerIds { get; set; }

    [JsonPropertyName("targetFrameworks")]
    public ISet<string> TargetFrameworks { get; set; }
}
