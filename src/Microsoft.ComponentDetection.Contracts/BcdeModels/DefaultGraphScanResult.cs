#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class DefaultGraphScanResult : ScanResult
{
    public DependencyGraphCollection DependencyGraphs { get; set; }
}
