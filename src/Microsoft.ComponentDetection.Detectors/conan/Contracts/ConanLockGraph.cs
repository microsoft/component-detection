namespace Microsoft.ComponentDetection.Detectors.Conan.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class ConanLockGraph
{
    [JsonPropertyName("revisions_enabled")]
    public bool RevisionsEnabled { get; set; }

    [JsonPropertyName("nodes")]
    public Dictionary<string, ConanLockNode> Nodes { get; set; }

    public override bool Equals(object obj) => obj is ConanLockGraph graph && this.RevisionsEnabled == graph.RevisionsEnabled && this.Nodes.Count == graph.Nodes.Count && !this.Nodes.Except(graph.Nodes).Any();

    public override int GetHashCode() => HashCode.Combine(this.RevisionsEnabled, this.Nodes);
}
