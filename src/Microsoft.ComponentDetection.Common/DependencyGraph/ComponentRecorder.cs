using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Common.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b101e980bad6a4194bcaf85cf037aecbe8b1fca61429ad511862c91be758742390c40ecc64c3a664103b071f6b3a563dd18c460c98f74a4fe2eaca8ca2672c777aec1a2d4672e3e4c0fb005548fe4a39c9fa48c8b6d094444dc45b02c4f9bf1fa7b3b91cdbe4921717869973a8d96f4f3a371f22ed03ff9b700f1534c014d5cb")]

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
            return singleFileRecorders.Select(x => x.GetComponent(componentId)?.Component).Where(x => x != null).FirstOrDefault();
        }

        public IEnumerable<DetectedComponent> GetDetectedComponents()
        {
            IEnumerable<DetectedComponent> detectedComponents;
            if (singleFileRecorders == null)
            {
                return Enumerable.Empty<DetectedComponent>();
            }

            detectedComponents = singleFileRecorders
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

            var matching = singleFileRecorders.FirstOrDefault(x => x.ManifestFileLocation == location);
            if (matching == null)
            {
                matching = new SingleFileComponentRecorder(location, this, enableManualTrackingOfExplicitReferences, log);
                singleFileRecorders.Add(matching);
            }

            return matching;
        }

        internal DependencyGraph GetDependencyGraphForLocation(string location)
        {
            return singleFileRecorders.Single(x => x.ManifestFileLocation == location).DependencyGraph;
        }

        public IReadOnlyDictionary<string, IDependencyGraph> GetDependencyGraphsByLocation()
        {
            return singleFileRecorders.Where(x => x.DependencyGraph.HasComponents())
                .ToImmutableDictionary(x => x.ManifestFileLocation, x => x.DependencyGraph as IDependencyGraph);
        }

        public class SingleFileComponentRecorder : ISingleFileComponentRecorder
        {
            private readonly ILogger log;

            public string ManifestFileLocation { get; }

            internal DependencyGraph DependencyGraph { get; }

            IDependencyGraph ISingleFileComponentRecorder.DependencyGraph => DependencyGraph;

            private readonly ConcurrentDictionary<string, DetectedComponent> detectedComponentsInternal = new ConcurrentDictionary<string, DetectedComponent>();

            private readonly ComponentRecorder recorder;

            private object registerUsageLock = new object();

            public SingleFileComponentRecorder(string location, ComponentRecorder recorder, bool enableManualTrackingOfExplicitReferences, ILogger log)
            {
                ManifestFileLocation = location;
                this.recorder = recorder;
                this.log = log;
                DependencyGraph = new DependencyGraph(enableManualTrackingOfExplicitReferences);
            }

            public DetectedComponent GetComponent(string componentId)
            {
                if (detectedComponentsInternal.TryGetValue(componentId, out var detectedComponent))
                {
                    return detectedComponent;
                }

                return null;
            }

            public IReadOnlyDictionary<string, DetectedComponent> GetDetectedComponents()
            {
                // Should this be immutable?
                return detectedComponentsInternal;
            }

            public void RegisterUsage(
                DetectedComponent detectedComponent,
                bool isExplicitReferencedDependency = false,
                string parentComponentId = null,
                bool? isDevelopmentDependency = null)
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
                    log?.LogWarning("Detector should not populate DetectedComponent.FilePaths!");
                }

                if (detectedComponent.DependencyRoots?.Any() ?? false)
                {
                    log?.LogWarning("Detector should not populate DetectedComponent.DependencyRoots!");
                }

                if (detectedComponent.DevelopmentDependency.HasValue)
                {
                    log?.LogWarning("Detector should not populate DetectedComponent.DevelopmentDependency!");
                }
#endif

                string componentId = detectedComponent.Component.Id;
                DetectedComponent storedComponent = null;
                lock (registerUsageLock)
                {
                    storedComponent = detectedComponentsInternal.GetOrAdd(componentId, detectedComponent);
                    AddComponentToGraph(ManifestFileLocation, detectedComponent, isExplicitReferencedDependency, parentComponentId, isDevelopmentDependency);
                }
            }

            public void AddAdditionalRelatedFile(string relatedFilePath)
            {
                DependencyGraph.AddAdditionalRelatedFile(relatedFilePath);
            }

            public IList<string> GetAdditionalRelatedFiles()
            {
                return DependencyGraph.GetAdditionalRelatedFiles().ToImmutableList();
            }

            public IComponentRecorder GetParentComponentRecorder()
            {
                return recorder;
            }

            private void AddComponentToGraph(string location, DetectedComponent detectedComponent, bool isExplicitReferencedDependency, string parentComponentId, bool? isDevelopmentDependency)
            {
                var componentNode = new DependencyGraph.ComponentRefNode
                {
                    Id = detectedComponent.Component.Id,
                    IsExplicitReferencedDependency = isExplicitReferencedDependency,
                    IsDevelopmentDependency = isDevelopmentDependency,
                };

                DependencyGraph.AddComponent(componentNode, parentComponentId);
            }
        }
    }
}
