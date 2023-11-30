namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class DefaultGraphTranslationService : IGraphTranslationService
{
    private readonly ILogger<DefaultGraphTranslationService> logger;

    public DefaultGraphTranslationService(ILogger<DefaultGraphTranslationService> logger) => this.logger = logger;

    public ScanResult GenerateScanResultFromProcessingResult(
        DetectorProcessingResult detectorProcessingResult,
        ScanSettings settings,
        bool updateLocations = true)
    {
        var recorderDetectorPairs = detectorProcessingResult.ComponentRecorders;

        var unmergedComponents = this.GatherSetOfDetectedComponentsUnmerged(recorderDetectorPairs, settings.SourceDirectory, updateLocations);

        var mergedComponents = this.FlattenAndMergeComponents(unmergedComponents);

        this.LogComponentScopeTelemetry(mergedComponents);

        return new DefaultGraphScanResult
        {
            ComponentsFound = mergedComponents.Select(x => this.ConvertToContract(x)).ToList(),
            ContainerDetailsMap = detectorProcessingResult.ContainersDetailsMap,
            DependencyGraphs = GraphTranslationUtility.AccumulateAndConvertToContract(recorderDetectorPairs
                .Select(tuple => tuple.Recorder)
                .Where(x => x != null)
                .Select(x => x.GetDependencyGraphsByLocation())),
            SourceDirectory = settings.SourceDirectory.ToString(),
        };
    }

    private void LogComponentScopeTelemetry(List<DetectedComponent> components)
    {
        using var record = new DetectedComponentScopeRecord();
        Parallel.ForEach(components, x =>
        {
            if (x.Component.Type.Equals(ComponentType.Maven)
                && x.DependencyScope.HasValue
                && (x.DependencyScope.Equals(DependencyScope.MavenProvided) || x.DependencyScope.Equals(DependencyScope.MavenSystem)))
            {
                record.IncrementProvidedScopeCount();
            }
        });
    }

    private IEnumerable<DetectedComponent> GatherSetOfDetectedComponentsUnmerged(IEnumerable<(IComponentDetector Detector, ComponentRecorder Recorder)> recorderDetectorPairs, DirectoryInfo rootDirectory, bool updateLocations)
    {
        return recorderDetectorPairs
            .Where(recorderDetectorPair => recorderDetectorPair.Recorder != null)
            .SelectMany(recorderDetectorPair =>
            {
                var detector = recorderDetectorPair.Detector;
                var componentRecorder = recorderDetectorPair.Recorder;
                var detectedComponents = componentRecorder.GetDetectedComponents();
                var dependencyGraphsByLocation = componentRecorder.GetDependencyGraphsByLocation();

                // Note that it looks like we are building up detected components functionally, but they are not immutable -- the code is just written
                //  to look like a pipeline.
                foreach (var component in detectedComponents)
                {
                    // clone custom locations and make them relative to root.
                    var declaredRawFilePaths = component.FilePaths ?? new HashSet<string>();
                    var componentCustomLocations = JsonConvert.DeserializeObject<HashSet<string>>(JsonConvert.SerializeObject(declaredRawFilePaths));
                    component.FilePaths?.Clear();

                    // Information about each component is relative to all of the graphs it is present in, so we take all graphs containing a given component and apply the graph data.
                    foreach (var graphKvp in dependencyGraphsByLocation.Where(x => x.Value.Contains(component.Component.Id)))
                    {
                        var location = graphKvp.Key;
                        var dependencyGraph = graphKvp.Value;

                        // Calculate roots of the component
                        this.AddRootsToDetectedComponent(component, dependencyGraph, componentRecorder);
                        component.DevelopmentDependency = this.MergeDevDependency(component.DevelopmentDependency, dependencyGraph.IsDevelopmentDependency(component.Component.Id));
                        component.DependencyScope = DependencyScopeComparer.GetMergedDependencyScope(component.DependencyScope, dependencyGraph.GetDependencyScope(component.Component.Id));
                        component.DetectedBy = detector;

                        // Return in a format that allows us to add the additional files for the components
                        var locations = dependencyGraph.GetAdditionalRelatedFiles();

                        // Experiments uses this service to build the dependency graph for analysis. In this case, we do not want to update the locations of the component.
                        // Updating the locations of the component will propogate to the final depenendcy graph and cause the graph to be incorrect.
                        if (updateLocations)
                        {
                            // graph authoritatively stores the location of the component
                            locations.Add(location);

                            foreach (var customLocation in componentCustomLocations)
                            {
                                locations.Add(customLocation);
                            }

                            var relativePaths = this.MakeFilePathsRelative(this.logger, rootDirectory, locations);

                            foreach (var additionalRelatedFile in relativePaths ?? Enumerable.Empty<string>())
                            {
                                component.AddComponentFilePath(additionalRelatedFile);
                            }
                        }
                    }
                }

                return detectedComponents;
            }).ToList();
    }

    private List<DetectedComponent> FlattenAndMergeComponents(IEnumerable<DetectedComponent> components)
    {
        var flattenedAndMergedComponents = new List<DetectedComponent>();
        foreach (var grouping in components.GroupBy(x => x.Component.Id + x.DetectedBy.Id))
        {
            flattenedAndMergedComponents.Add(this.MergeComponents(grouping));
        }

        return flattenedAndMergedComponents;
    }

    private bool? MergeDevDependency(bool? left, bool? right)
    {
        if (left == null)
        {
            return right;
        }

        if (right != null)
        {
            return left.Value && right.Value;
        }

        return left;
    }

    private DetectedComponent MergeComponents(IEnumerable<DetectedComponent> enumerable)
    {
        if (enumerable.Count() == 1)
        {
            return enumerable.First();
        }

        // Multiple detected components for the same logical component id -- this happens when different files see the same component. This code should go away when we get all
        //  mutable data out of detected component -- we can just take any component.
        var firstComponent = enumerable.First();
        foreach (var nextComponent in enumerable.Skip(1))
        {
            foreach (var filePath in nextComponent.FilePaths ?? Enumerable.Empty<string>())
            {
                firstComponent.AddComponentFilePath(filePath);
            }

            foreach (var root in nextComponent.DependencyRoots ?? Enumerable.Empty<TypedComponent>())
            {
                firstComponent.DependencyRoots.Add(root);
            }

            firstComponent.DevelopmentDependency = this.MergeDevDependency(firstComponent.DevelopmentDependency, nextComponent.DevelopmentDependency);
            firstComponent.DependencyScope = DependencyScopeComparer.GetMergedDependencyScope(firstComponent.DependencyScope, nextComponent.DependencyScope);

            if (nextComponent.ContainerDetailIds.Count > 0)
            {
                foreach (var containerDetailId in nextComponent.ContainerDetailIds)
                {
                    firstComponent.ContainerDetailIds.Add(containerDetailId);
                }
            }
        }

        return firstComponent;
    }

    private void AddRootsToDetectedComponent(DetectedComponent detectedComponent, IDependencyGraph dependencyGraph, IComponentRecorder componentRecorder)
    {
        detectedComponent.DependencyRoots ??= new HashSet<TypedComponent>(new ComponentComparer());

        if (dependencyGraph == null)
        {
            return;
        }

        var roots = dependencyGraph.GetExplicitReferencedDependencyIds(detectedComponent.Component.Id);

        foreach (var rootId in roots)
        {
            detectedComponent.DependencyRoots.Add(componentRecorder.GetComponent(rootId));
        }
    }

    private HashSet<string> MakeFilePathsRelative(ILogger logger, DirectoryInfo rootDirectory, HashSet<string> filePaths)
    {
        if (rootDirectory == null)
        {
            return null;
        }

        // Make relative Uri needs a trailing separator to ensure that we turn "directory we are scanning" into "/"
        var rootDirectoryFullName = rootDirectory.FullName;
        if (!rootDirectory.FullName.EndsWith(Path.DirectorySeparatorChar.ToString()) && !rootDirectory.FullName.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
        {
            rootDirectoryFullName += Path.DirectorySeparatorChar;
        }

        var rootUri = new Uri(rootDirectoryFullName);
        var relativePathSet = new HashSet<string>();
        foreach (var path in filePaths)
        {
            try
            {
                var relativePath = rootUri.MakeRelativeUri(new Uri(path)).ToString();
                if (!relativePath.StartsWith("/"))
                {
                    relativePath = "/" + relativePath;
                }

                relativePathSet.Add(relativePath);
            }
            catch (UriFormatException e)
            {
                logger.LogDebug(e, "The path: {Path} could not be resolved relative to the root {RootUri}", path, rootUri);
            }
        }

        return relativePathSet;
    }

    private ScannedComponent ConvertToContract(DetectedComponent component)
    {
        return new ScannedComponent
        {
            DetectorId = component.DetectedBy.Id,
            IsDevelopmentDependency = component.DevelopmentDependency,
            DependencyScope = component.DependencyScope,
            LocationsFoundAt = component.FilePaths,
            Component = component.Component,
            TopLevelReferrers = component.DependencyRoots,
            ContainerDetailIds = component.ContainerDetailIds,
            ContainerLayerIds = component.ContainerLayerIds,
        };
    }
}
