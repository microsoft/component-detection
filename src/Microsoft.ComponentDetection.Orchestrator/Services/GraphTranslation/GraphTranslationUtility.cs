using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation
{
    public static class GraphTranslationUtility
    {
        public static DependencyGraphCollection AccumulateAndConvertToContract(IEnumerable<IReadOnlyDictionary<string, IDependencyGraph>> dependencyGraphs)
        {
            if (dependencyGraphs == null)
            {
                return null;
            }

            var model = new DependencyGraphCollection();
            foreach (var graphsByLocation in dependencyGraphs)
            {
                foreach (var graphByLocation in graphsByLocation)
                {
                    if (!model.TryGetValue(graphByLocation.Key, out var graphWithMetadata))
                    {
                        model[graphByLocation.Key] = graphWithMetadata = new DependencyGraphWithMetadata
                        {
                            ExplicitlyReferencedComponentIds = new HashSet<string>(),
                            Graph = new DependencyGraph(),
                            DevelopmentDependencies = new HashSet<string>(),
                            Dependencies = new HashSet<string>(),
                        };
                    }

                    foreach (var componentId in graphByLocation.Value.GetComponents())
                    {
                        var componentDependencies = graphByLocation.Value.GetDependenciesForComponent(componentId);

                        // We set dependencies to null basically to make the serialized output look more consistent (instead of empty arrays). If another location gets merged that has dependencies, it needs to create and set the key to non-null.
                        if (!graphWithMetadata.Graph.TryGetValue(componentId, out var dependencies))
                        {
                            dependencies = componentDependencies != null && componentDependencies.Any() ? new HashSet<string>() : null;
                            graphWithMetadata.Graph[componentId] = dependencies;
                        }
                        else if (dependencies == null && componentDependencies != null && componentDependencies.Any())
                        {
                            // The explicit case where new data is found in a later graph for a given component at a location, and it is adding dependencies.
                            graphWithMetadata.Graph[componentId] = dependencies = new HashSet<string>();
                        }

                        foreach (var dependentComponentId in componentDependencies)
                        {
                            dependencies.Add(dependentComponentId);
                        }

                        if (graphByLocation.Value.IsComponentExplicitlyReferenced(componentId))
                        {
                            graphWithMetadata.ExplicitlyReferencedComponentIds.Add(componentId);
                        }

                        var isDevelopmentDependency = graphByLocation.Value.IsDevelopmentDependency(componentId);
                        if (isDevelopmentDependency.HasValue)
                        {
                            if (isDevelopmentDependency == false)
                            {
                                graphWithMetadata.Dependencies.Add(componentId);
                            }
                            else
                            {
                                graphWithMetadata.DevelopmentDependencies.Add(componentId);
                            }
                        }
                    }
                }
            }

            return model;
        }
    }
}
