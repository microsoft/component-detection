#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class Detector
{
    [JsonPropertyName("detectorId")]
    public string DetectorId { get; set; }

    [JsonPropertyName("isExperimental")]
    public bool IsExperimental { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("supportedComponentTypes")]
    public IEnumerable<ComponentType> SupportedComponentTypes { get; set; }
}
