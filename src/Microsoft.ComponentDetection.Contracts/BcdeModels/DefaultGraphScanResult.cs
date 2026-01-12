#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Text.Json.Serialization;

public class DefaultGraphScanResult : ScanResult
{
    [JsonPropertyName("dependencyGraphs")]
    public DependencyGraphCollection DependencyGraphs { get; set; }
}
