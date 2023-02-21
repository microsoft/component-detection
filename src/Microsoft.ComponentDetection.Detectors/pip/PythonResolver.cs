namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class PythonResolver : IPythonResolver
{
    private readonly IPyPiClient pypiClient;
    private readonly ILogger logger;

    public PythonResolver(IPyPiClient pypiClient, ILogger logger)
    {
        this.pypiClient = pypiClient;
        this.logger = logger;
    }

    /// <summary>
    /// Resolves the root Python packages from the initial list of packages.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    /// <param name="initialPackages">The initial list of packages.</param>
    /// <returns>The root packages, with dependencies associated as children.</returns>
    public async Task<IList<PipGraphNode>> ResolveRootsAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IList<PipDependencySpecification> initialPackages)
    {
        var state = new PythonResolverState();

        // Fill the dictionary with valid packages for the roots
        foreach (var rootPackage in initialPackages)
        {
            // If we have it, we probably just want to skip at this phase as this indicates duplicates
            if (!state.ValidVersionMap.TryGetValue(rootPackage.Name, out _))
            {
                var result = await this.pypiClient.GetReleasesAsync(rootPackage);

                if (result.Keys.Any())
                {
                    state.ValidVersionMap[rootPackage.Name] = result;

                    // Grab the latest version as our candidate version
                    var candidateVersion = state.ValidVersionMap[rootPackage.Name].Keys.Any()
                        ? state.ValidVersionMap[rootPackage.Name].Keys.Last() : null;

                    var node = new PipGraphNode(new PipComponent(rootPackage.Name, candidateVersion));

                    state.NodeReferences[rootPackage.Name] = node;

                    state.Roots.Add(node);

                    state.ProcessingQueue.Enqueue((rootPackage.Name, rootPackage));
                }
                else
                {
                    this.logger.LogWarning(
                        "Root dependency {RootPackageName} not found on pypi. Skipping package.",
                        rootPackage.Name);
                    singleFileComponentRecorder.RegisterPackageParseFailure(rootPackage.Name);
                }
            }
        }

        // Now queue packages for processing
        return await this.ProcessQueueAsync(singleFileComponentRecorder, state) ?? new List<PipGraphNode>();
    }

    private async Task<IList<PipGraphNode>> ProcessQueueAsync(ISingleFileComponentRecorder singleFileComponentRecorder, PythonResolverState state)
    {
        while (state.ProcessingQueue.Count > 0)
        {
            var (root, currentNode) = state.ProcessingQueue.Dequeue();

            // gather all dependencies for the current node
            var dependencies = (await this.FetchPackageDependenciesAsync(state, currentNode)).Where(x => !x.PackageIsUnsafe());

            foreach (var dependencyNode in dependencies)
            {
                // if we have already seen the dependency and the version we have is valid, just add the dependency to the graph
                if (state.NodeReferences.TryGetValue(dependencyNode.Name, out var node) &&
                    PythonVersionUtilities.VersionValidForSpec(node.Value.Version, dependencyNode.DependencySpecifiers))
                {
                    state.NodeReferences[currentNode.Name].Children.Add(node);
                    node.Parents.Add(state.NodeReferences[currentNode.Name]);
                }
                else if (node != null)
                {
                    this.logger.LogWarning("Candidate version ({NodeValueId}) for {DependencyName} already exists in map and the version is NOT valid.", node.Value.Id, dependencyNode.Name);
                    this.logger.LogWarning("Specifiers: {DependencySpecifiers} for package {CurrentNodeName} caused this.", string.Join(',', dependencyNode.DependencySpecifiers), currentNode.Name);

                    // The currently selected version is invalid, try to see if there is another valid version available
                    if (!await this.InvalidateAndReprocessAsync(state, node, dependencyNode))
                    {
                        this.logger.LogWarning(
                            "Version Resolution for {DependencyName} failed, assuming last valid version is used.",
                            dependencyNode.Name);

                        // there is no valid version available for the node, dependencies are incompatible,
                    }
                }
                else
                {
                    // We haven't encountered this package before, so let's fetch it and find a candidate
                    var result = await this.pypiClient.GetReleasesAsync(dependencyNode);

                    if (result.Keys.Any())
                    {
                        state.ValidVersionMap[dependencyNode.Name] = result;
                        var candidateVersion = state.ValidVersionMap[dependencyNode.Name].Keys.Any()
                            ? state.ValidVersionMap[dependencyNode.Name].Keys.Last() : null;

                        this.AddGraphNode(state, state.NodeReferences[currentNode.Name], dependencyNode.Name, candidateVersion);

                        state.ProcessingQueue.Enqueue((root, dependencyNode));
                    }
                    else
                    {
                        this.logger.LogWarning(
                            "Dependency Package {DependencyName} not found in Pypi. Skipping package",
                            dependencyNode.Name);
                        singleFileComponentRecorder.RegisterPackageParseFailure(dependencyNode.Name);
                    }
                }
            }
        }

        return state.Roots;
    }

    private async Task<bool> InvalidateAndReprocessAsync(
        PythonResolverState state,
        PipGraphNode node,
        PipDependencySpecification newSpec)
    {
        var pipComponent = node.Value;

        var oldVersions = state.ValidVersionMap[pipComponent.Name].Keys.ToList();
        var currentSelectedVersion = node.Value.Version;
        var currentReleases = state.ValidVersionMap[pipComponent.Name][currentSelectedVersion];
        foreach (var version in oldVersions)
        {
            if (!PythonVersionUtilities.VersionValidForSpec(version, newSpec.DependencySpecifiers))
            {
                state.ValidVersionMap[pipComponent.Name].Remove(version);
            }
        }

        if (state.ValidVersionMap[pipComponent.Name].Count == 0)
        {
            state.ValidVersionMap[pipComponent.Name][currentSelectedVersion] = currentReleases;
            return false;
        }

        var candidateVersion = state.ValidVersionMap[pipComponent.Name].Keys.Any() ? state.ValidVersionMap[pipComponent.Name].Keys.Last() : null;

        node.Value = new PipComponent(pipComponent.Name, candidateVersion);

        var dependencies = (await this.FetchPackageDependenciesAsync(state, newSpec)).ToDictionary(x => x.Name, x => x);

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

    private async Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(
        PythonResolverState state,
        PipDependencySpecification spec)
    {
        var candidateVersion = state.NodeReferences[spec.Name].Value.Version;

        var packageToFetch = state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_wheel", x.PackageType, StringComparison.OrdinalIgnoreCase)) ??
                             state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_egg", x.PackageType, StringComparison.OrdinalIgnoreCase));
        if (packageToFetch == null)
        {
            return new List<PipDependencySpecification>();
        }

        return await this.pypiClient.FetchPackageDependenciesAsync(spec.Name, candidateVersion, packageToFetch);
    }

    private void AddGraphNode(PythonResolverState state, PipGraphNode parent, string name, string version)
    {
        if (state.NodeReferences.TryGetValue(name, out var value))
        {
            parent.Children.Add(value);
            value.Parents.Add(parent);
        }
        else
        {
            var node = new PipGraphNode(new PipComponent(name, version));
            state.NodeReferences[name] = node;
            parent.Children.Add(node);
            node.Parents.Add(parent);
        }
    }

    private class PythonResolverState
    {
        public IDictionary<string, SortedDictionary<string, IList<PythonProjectRelease>>> ValidVersionMap { get; }
            = new Dictionary<string, SortedDictionary<string, IList<PythonProjectRelease>>>(StringComparer.OrdinalIgnoreCase);

        public Queue<(string PackageName, PipDependencySpecification Package)> ProcessingQueue { get; } = new Queue<(string, PipDependencySpecification)>();

        public IDictionary<string, PipGraphNode> NodeReferences { get; } = new Dictionary<string, PipGraphNode>(StringComparer.OrdinalIgnoreCase);

        public IList<PipGraphNode> Roots { get; } = new List<PipGraphNode>();
    }
}
