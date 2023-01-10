using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class DependencyGraphWithMetadata
{
    public DependencyGraph Graph { get; set; }

    public HashSet<string> ExplicitlyReferencedComponentIds { get; set; }

    public HashSet<string> DevelopmentDependencies { get; set; }

    public HashSet<string> Dependencies { get; set; }
}
