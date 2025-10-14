#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class Detector
{
    public string DetectorId { get; set; }

    public bool IsExperimental { get; set; }

    public int Version { get; set; }

    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public IEnumerable<ComponentType> SupportedComponentTypes { get; set; }
}
