#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Conan.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ConanLockGraph
{
    [JsonPropertyName("revisions_enabled")]
    public bool RevisionsEnabled { get; set; }

    [JsonPropertyName("nodes")]
    public Dictionary<string, ConanLockNode> Nodes { get; set; }
}
