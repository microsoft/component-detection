#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class Detector
{
    [JsonPropertyName("detectorId")]
    public string DetectorId { get; set; }

    [JsonPropertyName("isExperimental")]
    public bool IsExperimental { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    [JsonPropertyName("supportedComponentTypes")]
    public IEnumerable<ComponentType> SupportedComponentTypes { get; set; }
}
