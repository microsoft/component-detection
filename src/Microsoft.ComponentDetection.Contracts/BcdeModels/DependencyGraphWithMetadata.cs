#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class DependencyGraphWithMetadata
{
    [JsonPropertyName("graph")]
    public DependencyGraph Graph { get; set; }

    [JsonPropertyName("explicitlyReferencedComponentIds")]
    public HashSet<string> ExplicitlyReferencedComponentIds { get; set; }

    [JsonPropertyName("developmentDependencies")]
    public HashSet<string> DevelopmentDependencies { get; set; }

    [JsonPropertyName("dependencies")]
    public HashSet<string> Dependencies { get; set; }
}
