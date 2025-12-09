#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ScanResult
{
    [JsonPropertyName("componentsFound")]
    public IEnumerable<ScannedComponent> ComponentsFound { get; set; }

    [JsonPropertyName("detectorsInScan")]
    public IEnumerable<Detector> DetectorsInScan { get; set; }

    [JsonPropertyName("detectorsNotInScan")]
    public IEnumerable<Detector> DetectorsNotInScan { get; set; }

    [JsonPropertyName("containerDetailsMap")]
    public Dictionary<int, ContainerDetails> ContainerDetailsMap { get; set; }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [JsonPropertyName("resultCode")]
    public ProcessingResultCode ResultCode { get; set; }

    [JsonPropertyName("sourceDirectory")]
    public string SourceDirectory { get; set; }
}
