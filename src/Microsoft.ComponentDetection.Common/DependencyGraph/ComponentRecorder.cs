using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Common.Tests")]

namespace Microsoft.ComponentDetection.Common.DependencyGraph
{
    public class ComponentRecorder : IComponentRecorder
    {
        private readonly ILogger log;

        private readonly ConcurrentBag<SingleFileComponentRecorder> singleFileRecorders = new ConcurrentBag<SingleFileComponentRecorder>();

        private readonly bool enableManualTrackingOfExplicitReferences;

        public ComponentRecorder(ILogger log = null, bool enableManualTrackingOfExplicitReferences = true)
        {
            this.log = log;
            this.enableManualTrackingOfExplicitReferences = enableManualTrackingOfExplicitReferences;
        }

        public TypedComponent GetComponent(string componentId)
        {
            return this.singleFileRecorders.Select(x => x.GetComponent(componentId)?.Component).Where(x => x != null).FirstOrDefault();
        }

        public IEnumerable<DetectedComponent> GetDetectedComponents()
        {
            IEnumerable<DetectedComponent> detectedComponents;
            if (this.singleFileRecorders == null)
            {
                return Enumerable.Empty<DetectedComponent>();
            }

            detectedComponents = this.singleFileRecorders
                .Select(singleFileRecorder => singleFileRecorder.GetDetectedComponents().Values)
                .SelectMany(x => x)
                .GroupBy(x => x.Component.Id)
                .Select(grouping =>
                {
                    // We pick a winner here -- any stateful props could get lost at this point. Only stateful prop still outstanding is ContainerDetails.
                    var winningDetectedComponent = grouping.First();
                    foreach (var component in grouping)
                    {
                        foreach (var containerDetailId in component.ContainerDetailIds)
                        {
                            winningDetectedComponent.ContainerDetailIds.Add(containerDetailId);
                        }
                    }

                    return winningDetectedComponent;
                })
                .ToImmutableList();

            return detectedComponents;
        }

        public ISingleFileComponentRecorder CreateSingleFileComponentRecorder(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentNullException(nameof(location));
            }

            var matching = this.singleFileRecorders.FirstOrDefault(x => x.ManifestFileLocation == location);
            if (matching == null)
            {
                matching = new SingleFileComponentRecorder(location, this, this.enableManualTrackingOfExplicitReferences, this.log);
                this.singleFileRecorders.Add(matching);
            }

            return matching;
        }

        public IReadOnlyDictionary<string, IDependencyGraph> GetDependencyGraphsByLocation()
        {
            return this.singleFileRecorders.Where(x => x.DependencyGraph.HasComponents())
                .ToImmutableDictionary(x => x.ManifestFileLocation, x => x.DependencyGraph as IDependencyGraph);
        }

        internal DependencyGraph GetDependencyGraphForLocation(string location)
        {
            return this.singleFileRecorders.Single(x => x.ManifestFileLocation == location).DependencyGraph;
        }

        public class SingleFileComponentRecorder : ISingleFileComponentRecorder
        {
            private readonly ILogger log;

            private readonly ConcurrentDictionary<string, DetectedComponent> detectedComponentsInternal = new ConcurrentDictionary<string, DetectedComponent>();

            private readonly ComponentRecorder recorder;

            private readonly object registerUsageLock = new object();

            public SingleFileComponentRecorder(string location, ComponentRecorder recorder, bool enableManualTrackingOfExplicitReferences, ILogger log)
            {
                this.ManifestFileLocation = location;
                this.recorder = recorder;
                this.log = log;
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

            public void RegisterUsage(
                DetectedComponent detectedComponent,
                bool isExplicitReferencedDependency = false,
                string parentComponentId = null,
                bool? isDevelopmentDependency = null,
                DependencyScope? dependencyScope = null)
            {
                if (detectedComponent == null)
                {
                    throw new ArgumentNullException(paramName: nameof(detectedComponent));
                }

                if (detectedComponent.Component == null)
                {
                    throw new ArgumentException(Resources.MissingComponentId);
                }

#if DEBUG
                if (detectedComponent.FilePaths?.Any() ?? false)
                {
                    this.log?.LogWarning("Detector should not populate DetectedComponent.FilePaths!");
                }

                if (detectedComponent.DependencyRoots?.Any() ?? false)
                {
                    this.log?.LogWarning("Detector should not populate DetectedComponent.DependencyRoots!");
                }

                if (detectedComponent.DevelopmentDependency.HasValue)
                {
                    this.log?.LogWarning("Detector should not populate DetectedComponent.DevelopmentDependency!");
                }
#endif

                var componentId = detectedComponent.Component.Id;
                DetectedComponent storedComponent = null;
                lock (this.registerUsageLock)
                {
                    storedComponent = this.detectedComponentsInternal.GetOrAdd(componentId, detectedComponent);
                    this.AddComponentToGraph(this.ManifestFileLocation, detectedComponent, isExplicitReferencedDependency, parentComponentId, isDevelopmentDependency, dependencyScope);
                }
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
}
