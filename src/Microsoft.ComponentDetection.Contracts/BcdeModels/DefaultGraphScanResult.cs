using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels
{
    [JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class DefaultGraphScanResult : ScanResult
    {
        public DependencyGraphCollection DependencyGraphs { get; set; }
    }
}
