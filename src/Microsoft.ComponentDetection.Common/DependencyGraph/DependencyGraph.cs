#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Common.Tests")]

namespace Microsoft.ComponentDetection.Common.DependencyGraph;

internal class DependencyGraph : IDependencyGraph
{
    private static readonly CompositeFormat MissingNodeFormat = CompositeFormat.Parse(Resources.MissingNodeInDependencyGraph);

    private readonly ConcurrentDictionary<string, ComponentRefNode> componentNodes;

    private readonly bool enableManualTrackingOfExplicitReferences;

    public DependencyGraph(bool enableManualTrackingOfExplicitReferences)
    {
        this.componentNodes = new ConcurrentDictionary<string, ComponentRefNode>();
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

        IList<string> explicitReferencedDependencyIds = [];

        this.GetExplicitReferencedDependencies(componentRef, explicitReferencedDependencyIds, new HashSet<string>());

        return explicitReferencedDependencyIds;
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

    public HashSet<TypedComponent> GetAncestorsAsTypedComponents(string componentId, Func<string, TypedComponent> toTypedComponent)
    {
        ArgumentNullException.ThrowIfNull(componentId);
        return this.GetAncestors(componentId)
            .Select(a => this.componentNodes.TryGetValue(a, out var component) ? component : null)
            .Where(a => a != null)
            .Select(a => a.TypedComponent ?? toTypedComponent(a.Id))
            .ToHashSet(new ComponentComparer());
    }

    public HashSet<TypedComponent> GetRootsAsTypedComponents(string componentId, Func<string, TypedComponent> toTypedComponent)
    {
        ArgumentNullException.ThrowIfNull(componentId);
        return this.GetExplicitReferencedDependencyIds(componentId)
            .Select(r => this.componentNodes.TryGetValue(r, out var component) ? component : null)
            .Where(r => r != null)
            .Select(r => r.TypedComponent ?? toTypedComponent(r.Id))
            .ToHashSet(new ComponentComparer());
    }

    public void FillTypedComponents(Func<string, TypedComponent> toTypedComponent)
    {
        foreach (var componentId in this.componentNodes.Values)
        {
            componentId.TypedComponent = toTypedComponent(componentId.Id);
        }
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

    private void GetExplicitReferencedDependencies(ComponentRefNode component, IList<string> explicitReferencedDependencyIds, ISet<string> visited)
    {
        if (this.IsExplicitReferencedDependency(component))
        {
            explicitReferencedDependencyIds.Add(component.Id);
        }

        visited.Add(component.Id);

        foreach (var parentId in component.DependedOnByIds)
        {
            if (!visited.Contains(parentId))
            {
                this.GetExplicitReferencedDependencies(this.componentNodes[parentId], explicitReferencedDependencyIds, visited);
            }
        }
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
        foreach (var parentId in componentRef.DependedOnByIds)
        {
            if (ancestors.ContainsKey(parentId))
            {
                continue;
            }

            ancestors.Add(parentId, depth);
            this.GetAncestorsRecursive(this.componentNodes[parentId], ancestors, depth + 1);
        }
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

        internal TypedComponent TypedComponent { get; set; }
    }
}
