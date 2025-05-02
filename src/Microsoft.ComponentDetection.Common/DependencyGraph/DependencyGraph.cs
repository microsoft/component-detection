using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Common.Tests")]

namespace Microsoft.ComponentDetection.Common.DependencyGraph;

internal class DependencyGraph : IDependencyGraph
{
    private static readonly CompositeFormat MissingNodeFormat = CompositeFormat.Parse(Resources.MissingNodeInDependencyGraph);

    private readonly ConcurrentDictionary<string, ComponentRefNode> componentNodes;

    private readonly bool enableManualTrackingOfExplicitReferences;

    private readonly ConcurrentDictionary<string, ISet<string>> rootsCache;

    private readonly ConcurrentDictionary<string, IDictionary<string, int>> ancestorsCache;

    public DependencyGraph(bool enableManualTrackingOfExplicitReferences)
    {
        this.componentNodes = new ConcurrentDictionary<string, ComponentRefNode>();
        this.rootsCache = new ConcurrentDictionary<string, ISet<string>>();
        this.ancestorsCache = new ConcurrentDictionary<string, IDictionary<string, int>>();
        this.enableManualTrackingOfExplicitReferences = enableManualTrackingOfExplicitReferences;
    }

    internal ConcurrentDictionary<string, byte> AdditionalRelatedFiles { get; } = new ConcurrentDictionary<string, byte>();

    public void AddComponent(ComponentRefNode componentNode, string parentComponentId = null)
    {
        ArgumentNullException.ThrowIfNull(componentNode);

        if (string.IsNullOrWhiteSpace(componentNode.Id))
        {
            throw new ArgumentNullException(nameof(componentNode), "Invalid component node id");
        }

        this.componentNodes.AddOrUpdate(componentNode.Id, componentNode, (key, currentNode) =>
        {
            currentNode.IsExplicitReferencedDependency |= componentNode.IsExplicitReferencedDependency;

            // If incoming component has a dev dependency value, and it with whatever is in storage. Otherwise, leave storage alone.
            if (componentNode.IsDevelopmentDependency.HasValue)
            {
                currentNode.IsDevelopmentDependency = currentNode.IsDevelopmentDependency.GetValueOrDefault(true) && componentNode.IsDevelopmentDependency.Value;
            }

            if (componentNode.DependencyScope.HasValue)
            {
                currentNode.DependencyScope = DependencyScopeComparer.GetMergedDependencyScope(currentNode.DependencyScope, componentNode.DependencyScope);
            }

            return currentNode;
        });

        this.AddDependency(componentNode.Id, parentComponentId);
    }

    public bool Contains(string componentId)
    {
        return this.componentNodes.ContainsKey(componentId);
    }

    public ICollection<string> GetDependenciesForComponent(string componentId)
    {
        return this.componentNodes[componentId].DependencyIds;
    }

    public ICollection<string> GetExplicitReferencedDependencyIds(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentNullException(nameof(componentId));
        }

        if (!this.componentNodes.TryGetValue(componentId, out var componentRef))
        {
            throw new ArgumentException(string.Format(null, MissingNodeFormat, componentId), paramName: nameof(componentId));
        }

        return this.GetExplicitReferencedDependencies(componentRef, new HashSet<string>()).ToList();
    }

    /// <summary>
    /// Any file added here will be reported as a location on ALL components found in current graph.
    /// </summary>
    public void AddAdditionalRelatedFile(string additionalRelatedFile)
    {
        this.AdditionalRelatedFiles.AddOrUpdate(additionalRelatedFile, 0, (notUsed, notUsed2) => 0);
    }

    public HashSet<string> GetAdditionalRelatedFiles()
    {
        return this.AdditionalRelatedFiles.Keys.ToHashSet();
    }

    public bool HasComponents()
    {
        return !this.componentNodes.IsEmpty;
    }

    public bool? IsDevelopmentDependency(string componentId)
    {
        return this.componentNodes[componentId].IsDevelopmentDependency;
    }

    public DependencyScope? GetDependencyScope(string componentId)
    {
        return this.componentNodes[componentId].DependencyScope;
    }

    public IEnumerable<string> GetAllExplicitlyReferencedComponents()
    {
        return this.componentNodes.Values
            .Where(componentRefNode => this.IsExplicitReferencedDependency(componentRefNode))
            .Select(componentRefNode => componentRefNode.Id);
    }

    public ICollection<string> GetAncestors(string componentId)
    {
        ArgumentNullException.ThrowIfNull(componentId);

        if (!this.componentNodes.TryGetValue(componentId, out var componentRef))
        {
            // this component isn't in the graph, so it has no ancestors
            return [];
        }

        // store the component id and the depth we found it at
        var ancestors = new Dictionary<string, int>();
        this.GetAncestorsRecursive(componentRef, ancestors, 1);
        return ancestors.OrderBy(x => x.Value)
            .Select(x => x.Key)
            .Where(x => !x.Equals(componentId))
            .ToList();
    }

    IEnumerable<string> IDependencyGraph.GetDependenciesForComponent(string componentId)
    {
        return this.GetDependenciesForComponent(componentId).ToImmutableList();
    }

    IEnumerable<string> IDependencyGraph.GetComponents()
    {
        return this.componentNodes.Keys.ToImmutableList();
    }

    bool IDependencyGraph.IsComponentExplicitlyReferenced(string componentId)
    {
        return this.IsExplicitReferencedDependency(this.componentNodes[componentId]);
    }

    private IEnumerable<string> GetExplicitReferencedDependencies(ComponentRefNode component, ISet<string> visited)
    {
        if (this.rootsCache.TryGetValue(component.Id, out var cachedExplicitReferencedDependencyIds))
        {
            return cachedExplicitReferencedDependencyIds;
        }

        if (this.IsExplicitReferencedDependency(component))
        {
            var explicitReferencedDependencyIdsSet = new HashSet<string>() { component.Id };
            this.rootsCache.TryAdd(component.Id, explicitReferencedDependencyIdsSet);
            return explicitReferencedDependencyIdsSet;
        }

        visited.Add(component.Id);

        IEnumerable<string> explicitReferencedDependencyIds = [];
        foreach (var parentId in component.DependedOnByIds)
        {
            if (!visited.Contains(parentId))
            {
                explicitReferencedDependencyIds = explicitReferencedDependencyIds.Concat(this.GetExplicitReferencedDependencies(this.componentNodes[parentId], visited));
            }
        }

        this.rootsCache.TryAdd(component.Id, explicitReferencedDependencyIds.ToHashSet());
        return explicitReferencedDependencyIds;
    }

    private bool IsExplicitReferencedDependency(ComponentRefNode component)
    {
        return (this.enableManualTrackingOfExplicitReferences && component.IsExplicitReferencedDependency) ||
               (!this.enableManualTrackingOfExplicitReferences && !component.DependedOnByIds.Any());
    }

    private void AddDependency(string componentId, string parentComponentId)
    {
        if (string.IsNullOrWhiteSpace(parentComponentId))
        {
            return;
        }

        if (!this.componentNodes.TryGetValue(parentComponentId, out var parentComponentRefNode))
        {
            throw new ArgumentException(string.Format(null, MissingNodeFormat, parentComponentId), nameof(parentComponentId));
        }

        parentComponentRefNode.DependencyIds.Add(componentId);
        this.componentNodes[componentId].DependedOnByIds.Add(parentComponentId);
    }

    private void GetAncestorsRecursive(ComponentRefNode componentRef, IDictionary<string, int> ancestors, int depth)
    {
        if (this.ancestorsCache.TryGetValue(componentRef.Id, out var cachedAncestors))
        {
            return;
        }

        foreach (var parentId in componentRef.DependedOnByIds)
        {
            if (ancestors.ContainsKey(parentId))
            {
                continue;
            }

            ancestors.Add(parentId, depth);
            this.GetAncestorsRecursive(this.componentNodes[parentId], ancestors, depth + 1);
        }

        this.ancestorsCache.TryAdd(componentRef.Id, ancestors);
    }

    internal class ComponentRefNode
    {
        internal ComponentRefNode()
        {
            this.DependencyIds = new HashSet<string>();
            this.DependedOnByIds = new HashSet<string>();
        }

        internal bool IsExplicitReferencedDependency { get; set; }

        internal string Id { get; set; }

        internal ISet<string> DependencyIds { get; private set; }

        internal ISet<string> DependedOnByIds { get; private set; }

        internal bool? IsDevelopmentDependency { get; set; }

        internal DependencyScope? DependencyScope { get; set; }
    }
}
