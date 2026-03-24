#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class DefaultGraphScanResult : ScanResult
{
    [JsonPropertyName("dependencyGraphs")]
    public DependencyGraphCollection DependencyGraphs { get; set; }
}
