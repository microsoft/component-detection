namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

internal class UvMetadata
{
    [JsonPropertyName("requires-dist")]
    public List<UvDependency> RequiresDist { get; set; } = [];

    [JsonPropertyName("requires-dev")]
    public Dictionary<string, List<UvDependency>> RequiresDev { get; set; } = [];

    public void Normalize()
    {
        this.RequiresDist = this.RequiresDist
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        var normalizedDev = new Dictionary<string, List<UvDependency>>(this.RequiresDev.Count, this.RequiresDev.Comparer);
        foreach (var (group, dependencies) in this.RequiresDev)
        {
            normalizedDev[group] = dependencies
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .ToList();
        }

        this.RequiresDev = normalizedDev;
    }
}
