#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;

public abstract class PythonResolverBase
{
    private readonly ILogger logger;

    internal PythonResolverBase(ILogger logger) => this.logger = logger;

    /// <summary>
    /// Given a state, node, and new spec, will reprocess a new valid version for the node.
    /// </summary>
    /// <param name="state">The PythonResolverState.</param>
    /// <param name="node">The PipGraphNode.</param>
    /// <param name="newSpec">The PipDependencySpecification.</param>
    /// <returns>Returns true if the node can be reprocessed else false.</returns>
    protected async Task<bool> InvalidateAndReprocessAsync(
        PythonResolverState state,
        PipGraphNode node,
        PipDependencySpecification newSpec)
    {
        var pipComponent = node.Value;

        var oldVersions = state.ValidVersionMap[pipComponent.Name].Keys.ToList();
        var currentSelectedVersion = node.Value.Version;
        var currentReleases = state.ValidVersionMap[pipComponent.Name][currentSelectedVersion];
        foreach (var version in oldVersions.Where(version => !PythonVersionUtilities.VersionValidForSpec(version, newSpec.DependencySpecifiers)))
        {
            state.ValidVersionMap[pipComponent.Name].Remove(version);
        }

        if (state.ValidVersionMap[pipComponent.Name].Count == 0)
        {
            state.ValidVersionMap[pipComponent.Name][currentSelectedVersion] = currentReleases;
            return false;
        }

        var candidateVersion = state.ValidVersionMap[pipComponent.Name].Keys.Count != 0 ? state.ValidVersionMap[pipComponent.Name].Keys.Last() : null;

        node.Value = new PipComponent(pipComponent.Name, candidateVersion, author: pipComponent.Author, license: pipComponent.License);

        var fetchedDependences = await this.FetchPackageDependenciesAsync(state, newSpec);
        var dependencies = this.ResolveDependencySpecifications(pipComponent, fetchedDependences);

        var toRemove = new List<PipGraphNode>();
        foreach (var child in node.Children)
        {
            var pipChild = child.Value;

            if (!dependencies.TryGetValue(pipChild.Name, out var newDependency))
            {
                toRemove.Add(child);
            }
            else if (!PythonVersionUtilities.VersionValidForSpec(pipChild.Version, newDependency.DependencySpecifiers))
            {
                if (!await this.InvalidateAndReprocessAsync(state, child, newDependency))
                {
                    return false;
                }
            }
        }

        foreach (var remove in toRemove)
        {
            node.Children.Remove(remove);
        }

        return true;
    }

    /// <summary>
    /// Multiple dependency specification versions can be given for a single package name.
    /// Until a better method is devised, choose the latest entry.
    /// See https://github.com/microsoft/component-detection/issues/963.
    /// </summary>
    /// <returns>Dictionary of package names to dependency version specifiers.</returns>
    public Dictionary<string, PipDependencySpecification> ResolveDependencySpecifications(PipComponent component, IList<PipDependencySpecification> fetchedDependences)
    {
        var dependencies = new Dictionary<string, PipDependencySpecification>();
        fetchedDependences.ForEach(d =>
        {
            if (!dependencies.TryAdd(d.Name, d))
            {
                this.logger.LogWarning(
                    "Duplicate package dependencies entry for component:{ComponentName} with dependency:{DependencyName}. Existing dependency specifiers: {ExistingSpecifiers}. New dependency specifiers: {NewSpecifiers}.",
                    component.Name,
                    d.Name,
                    JsonConvert.SerializeObject(dependencies[d.Name].DependencySpecifiers),
                    JsonConvert.SerializeObject(d.DependencySpecifiers));
                dependencies[d.Name] = d;
            }
        });

        return dependencies;
    }

    protected abstract Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(
        PythonResolverState state,
        PipDependencySpecification spec);
}
