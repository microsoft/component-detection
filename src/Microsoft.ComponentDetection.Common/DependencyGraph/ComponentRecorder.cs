#nullable disable

namespace Microsoft.ComponentDetection.Common.DependencyGraph;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class ComponentRecorder : IComponentRecorder
{
    private readonly ConcurrentDictionary<string, SingleFileComponentRecorder> singleFileRecorders = [];

    private readonly bool enableManualTrackingOfExplicitReferences;

    private readonly ILogger logger;

    public ComponentRecorder(ILogger logger = null, bool enableManualTrackingOfExplicitReferences = true)
    {
        this.logger = logger;
        this.enableManualTrackingOfExplicitReferences = enableManualTrackingOfExplicitReferences;
    }

    public TypedComponent GetComponent(string componentId)
    {
        return this.singleFileRecorders.Values.Select(x => x.GetComponent(componentId)?.Component).FirstOrDefault(x => x != null);
    }

    public IEnumerable<DetectedComponent> GetDetectedComponents()
    {
        if (this.singleFileRecorders == null)
        {
            return [];
        }

        var allComponents = this.singleFileRecorders.Values
            .SelectMany(singleFileRecorder => singleFileRecorder.GetDetectedComponents().Values);

        // When both rich and bare entries exist for the same BaseId, rich entries are used as merge targets for bare entries.
        var reconciledComponents = new List<DetectedComponent>();

        foreach (var baseIdGroup in allComponents.GroupBy(x => x.Component.BaseId))
        {
            var richEntries = new List<DetectedComponent>();
            var bareEntries = new List<DetectedComponent>();

            // Sub-group by full Id first: merge duplicates of the same Id (existing behavior).
            foreach (var idGroup in baseIdGroup.GroupBy(x => x.Component.Id))
            {
                var merged = MergeDetectedComponentGroup(idGroup);

                if (merged.Component.Id == merged.Component.BaseId)
                {
                    bareEntries.Add(merged);
                }
                else
                {
                    richEntries.Add(merged);
                }
            }

            if (richEntries.Count > 0 && bareEntries.Count > 0)
            {
                // Merge each bare entry's metadata into every rich entry, then drop the bare.
                foreach (var bare in bareEntries)
                {
                    foreach (var rich in richEntries)
                    {
                        MergeComponentMetadata(source: bare, target: rich);
                    }
                }

                reconciledComponents.AddRange(richEntries);
            }
            else
            {
                // No conflict: either all rich (different Ids kept separate) or all bare.
                reconciledComponents.AddRange(richEntries);
                reconciledComponents.AddRange(bareEntries);
            }
        }

        return reconciledComponents.ToArray();
    }

    /// <summary>
    /// Merges component-level metadata from <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    private static void MergeComponentMetadata(DetectedComponent source, DetectedComponent target)
    {
        target.ContainerDetailIds.UnionWith(source.ContainerDetailIds);

        foreach (var kvp in source.ContainerLayerIds)
        {
            if (target.ContainerLayerIds.TryGetValue(kvp.Key, out var existingLayers))
            {
                target.ContainerLayerIds[kvp.Key] = existingLayers.Union(kvp.Value).ToList();
            }
            else
            {
                target.ContainerLayerIds[kvp.Key] = kvp.Value.ToList();
            }
        }

        target.LicensesConcluded = MergeAndNormalizeLicenses(target.LicensesConcluded, source.LicensesConcluded);
        target.Suppliers = MergeAndNormalizeSuppliers(target.Suppliers, source.Suppliers);
    }

    private static IList<string> MergeAndNormalizeLicenses(IList<string> target, IList<string> source)
    {
        if (target == null && source == null)
        {
            return null;
        }

        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (target != null)
        {
            merged.UnionWith(target.Where(x => x != null));
        }

        if (source != null)
        {
            merged.UnionWith(source.Where(x => x != null));
        }

        return merged.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IList<ActorInfo> MergeAndNormalizeSuppliers(IList<ActorInfo> target, IList<ActorInfo> source)
    {
        if (target == null && source == null)
        {
            return null;
        }

        var merged = new HashSet<ActorInfo>();

        if (target != null)
        {
            merged.UnionWith(target.Where(s => s != null));
        }

        if (source != null)
        {
            merged.UnionWith(source.Where(s => s != null));
        }

        return merged.OrderBy(s => s.Name).ThenBy(s => s.Type).ToList();
    }

    /// <summary>
    /// Merges a group of <see cref="DetectedComponent"/>s that share the same <see cref="TypedComponent.Id"/>
    /// into a single entry.
    /// </summary>
    private static DetectedComponent MergeDetectedComponentGroup(IEnumerable<DetectedComponent> grouping)
    {
        var winner = grouping.First();

        foreach (var component in grouping.Skip(1))
        {
            MergeComponentMetadata(source: component, target: winner);
        }

        return winner;
    }

    public IEnumerable<string> GetSkippedComponents()
    {
        if (this.singleFileRecorders == null)
        {
            return [];
        }

        return this.singleFileRecorders.Values
            .SelectMany(x => x.GetSkippedComponents().Keys)
            .Distinct()
            .ToArray();
    }

    public ISingleFileComponentRecorder CreateSingleFileComponentRecorder(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentNullException(nameof(location));
        }

        return this.singleFileRecorders.GetOrAdd(location, loc => new SingleFileComponentRecorder(loc, this, this.enableManualTrackingOfExplicitReferences, this.logger));
    }

    public IReadOnlyDictionary<string, IDependencyGraph> GetDependencyGraphsByLocation()
    {
        return new ReadOnlyDictionary<string, IDependencyGraph>(
            this.singleFileRecorders.Values
                .Where(x => x.DependencyGraph.HasComponents())
                .ToDictionary(x => x.ManifestFileLocation, x => x.DependencyGraph as IDependencyGraph));
    }

    internal DependencyGraph GetDependencyGraphForLocation(string location)
    {
        return this.singleFileRecorders[location].DependencyGraph;
    }

    public sealed class SingleFileComponentRecorder : ISingleFileComponentRecorder
    {
        private readonly ConcurrentDictionary<string, DetectedComponent> detectedComponentsInternal = new ConcurrentDictionary<string, DetectedComponent>();

        /// <summary>
        /// Dictionary of components which had an error during parsing and a dummy data value that only allocates 1 byte.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> skippedComponentsInternal = new ConcurrentDictionary<string, byte>();

        private readonly ComponentRecorder recorder;
        private readonly ILogger logger;

        private readonly object registerUsageLock = new object();

        public SingleFileComponentRecorder(string location, ComponentRecorder recorder, bool enableManualTrackingOfExplicitReferences, ILogger logger)
        {
            this.ManifestFileLocation = location;
            this.recorder = recorder;
            this.logger = logger;
            this.DependencyGraph = new DependencyGraph(enableManualTrackingOfExplicitReferences);
        }

        public string ManifestFileLocation { get; }

        IDependencyGraph ISingleFileComponentRecorder.DependencyGraph => this.DependencyGraph;

        internal DependencyGraph DependencyGraph { get; }

        public DetectedComponent GetComponent(string componentId)
        {
            if (this.detectedComponentsInternal.TryGetValue(componentId, out var detectedComponent))
            {
                return detectedComponent;
            }

            return null;
        }

        public IReadOnlyDictionary<string, DetectedComponent> GetDetectedComponents()
        {
            // Should this be immutable?
            return this.detectedComponentsInternal;
        }

        public IReadOnlyDictionary<string, byte> GetSkippedComponents()
        {
            return this.skippedComponentsInternal;
        }

        public void RegisterUsage(
            DetectedComponent detectedComponent,
            bool isExplicitReferencedDependency = false,
            string parentComponentId = null,
            bool? isDevelopmentDependency = null,
            DependencyScope? dependencyScope = null,
            string targetFramework = null)
        {
            ArgumentNullException.ThrowIfNull(detectedComponent);

            if (detectedComponent.Component == null)
            {
                throw new ArgumentException(Resources.MissingComponentId);
            }

#if DEBUG
            if (detectedComponent.DependencyRoots?.Count == 0)
            {
                this.logger?.LogWarning("Detector should not populate DetectedComponent.DependencyRoots!");
            }

            if (detectedComponent.DevelopmentDependency.HasValue)
            {
                this.logger?.LogWarning("Detector should not populate DetectedComponent.DevelopmentDependency!");
            }
#endif

            var componentId = detectedComponent.Component.Id;
            var storedComponent = this.detectedComponentsInternal.GetOrAdd(componentId, detectedComponent);

            if (!string.IsNullOrWhiteSpace(targetFramework))
            {
                storedComponent.TargetFrameworks.Add(targetFramework.Trim());
            }

            lock (this.registerUsageLock)
            {
                this.AddComponentToGraph(this.ManifestFileLocation, detectedComponent, isExplicitReferencedDependency, parentComponentId, isDevelopmentDependency, dependencyScope);
            }
        }

        public void RegisterPackageParseFailure(string skippedComponent)
        {
            ArgumentNullException.ThrowIfNull(skippedComponent);

            _ = this.skippedComponentsInternal[skippedComponent] = default;
        }

        public void AddAdditionalRelatedFile(string relatedFilePath)
        {
            this.DependencyGraph.AddAdditionalRelatedFile(relatedFilePath);
        }

        public IList<string> GetAdditionalRelatedFiles()
        {
            return this.DependencyGraph.GetAdditionalRelatedFiles().ToImmutableList();
        }

        public IComponentRecorder GetParentComponentRecorder()
        {
            return this.recorder;
        }

        private void AddComponentToGraph(
            string location,
            DetectedComponent detectedComponent,
            bool isExplicitReferencedDependency,
            string parentComponentId,
            bool? isDevelopmentDependency,
            DependencyScope? dependencyScope)
        {
            var componentNode = new DependencyGraph.ComponentRefNode
            {
                Id = detectedComponent.Component.Id,
                IsExplicitReferencedDependency = isExplicitReferencedDependency,
                IsDevelopmentDependency = isDevelopmentDependency,
                DependencyScope = dependencyScope,
            };

            this.DependencyGraph.AddComponent(componentNode, parentComponentId);
        }
    }
}
