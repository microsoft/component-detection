using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Common.Tests")]

namespace Microsoft.ComponentDetection.Common.DependencyGraph
{
    internal class DependencyGraph : IDependencyGraph
    {
        private ConcurrentDictionary<string, ComponentRefNode> componentNodes;

        internal ConcurrentDictionary<string, byte> AdditionalRelatedFiles { get; } = new ConcurrentDictionary<string, byte>();

        private bool enableManualTrackingOfExplicitReferences;

        public DependencyGraph(bool enableManualTrackingOfExplicitReferences)
        {
            this.componentNodes = new ConcurrentDictionary<string, ComponentRefNode>();
            this.enableManualTrackingOfExplicitReferences = enableManualTrackingOfExplicitReferences;
        }

        public void AddComponent(ComponentRefNode componentNode, string parentComponentId = null)
        {
            if (componentNode == null)
            {
                throw new ArgumentNullException(nameof(componentNode));
            }

            if (string.IsNullOrWhiteSpace(componentNode.Id))
            {
                throw new ArgumentNullException(nameof(componentNode.Id));
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
                throw new ArgumentException(string.Format(Resources.MissingNodeInDependencyGraph, componentId), paramName: nameof(componentId));
            }

            IList<string> explicitReferencedDependencyIds = new List<string>();

            this.GetExplicitReferencedDependencies(componentRef, explicitReferencedDependencyIds, new HashSet<string>());

            return explicitReferencedDependencyIds;
        }

        public void AddAdditionalRelatedFile(string additionalRelatedFile)
        {
            this.AdditionalRelatedFiles.AddOrUpdate(additionalRelatedFile, 0, (notUsed, notUsed2) => 0);
        }

        public HashSet<string> GetAdditionalRelatedFiles()
        {
            return this.AdditionalRelatedFiles.Keys.ToImmutableHashSet().ToHashSet();
        }

        public bool HasComponents()
        {
            return this.componentNodes.Count > 0;
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
                throw new ArgumentException(string.Format(Resources.MissingNodeInDependencyGraph, parentComponentId), nameof(parentComponentId));
            }

            parentComponentRefNode.DependencyIds.Add(componentId);
            this.componentNodes[componentId].DependedOnByIds.Add(parentComponentId);
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

        internal class ComponentRefNode
        {
            internal bool IsExplicitReferencedDependency { get; set; }

            internal string Id { get; set; }

            internal ISet<string> DependencyIds { get; private set; }

            internal ISet<string> DependedOnByIds { get; private set; }

            internal bool? IsDevelopmentDependency { get; set; }

            internal DependencyScope? DependencyScope { get; set; }

            internal ComponentRefNode()
            {
                this.DependencyIds = new HashSet<string>();
                this.DependedOnByIds = new HashSet<string>();
            }
        }
    }
}
