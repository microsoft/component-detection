namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

[DataContract]
internal class UvMetadata
{
    [DataMember(Name = "requires-dist")]
    public List<UvDependency> RequiresDist { get; set; } = [];

    [DataMember(Name = "requires-dev")]
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
