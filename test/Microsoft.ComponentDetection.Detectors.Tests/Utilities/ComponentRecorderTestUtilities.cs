#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;

public static class ComponentRecorderTestUtilities
{
    public static void ForAllComponents(this IComponentRecorder recorder, Action<ComponentOrientedGrouping> forEachComponent)
    {
        var allComponents = recorder.GetDetectedComponents();
        var graphs = recorder.GetDependencyGraphsByLocation();

        // This magic grouping is a flattening of "occurrences" of components across single file recorders. This allows aggregate operations
        //  per component id, which is logical for most tests.
        var graphsAndLocationsByComponentId = GroupByComponentId(graphs);

        foreach (var item in graphsAndLocationsByComponentId)
        {
            forEachComponent(TupleToObject(item));
        }
    }

    public static void ForOneComponent(this IComponentRecorder recorder, string componentId, Action<ComponentOrientedGrouping> forOneComponent)
    {
        var allComponents = recorder.GetDetectedComponents();
        var graphs = recorder.GetDependencyGraphsByLocation();

        // This magic grouping is a flattening of "occurrences" of components across single file recorders. This allows aggregate operations
        //  per component id, which is logical for most tests.
        var graphsAndLocationsByComponentId = GroupByComponentId(graphs);

        forOneComponent(TupleToObject(graphsAndLocationsByComponentId.First(x => x.Key == componentId)));
    }

    public static bool? GetEffectiveDevDependencyValue(this IComponentRecorder recorder, string componentId)
    {
        bool? existingDevDepValue = null;
        recorder.ForOneComponent(componentId, grouping =>
        {
            foreach (var (manifestFile, graph) in grouping.FoundInGraphs)
            {
                var devDepValue = graph.IsDevelopmentDependency(componentId);
                if (!existingDevDepValue.HasValue)
                {
                    existingDevDepValue = devDepValue;
                }
                else if (devDepValue.HasValue)
                {
                    existingDevDepValue &= devDepValue;
                }
            }
        });

        return existingDevDepValue;
    }

    public static bool IsDependencyOfExplicitlyReferencedComponents<TTypedComponent>(
        this IComponentRecorder recorder,
        string componentIdToValidate,
        params Func<TTypedComponent, bool>[] locatingPredicatesForParentExplicitReference)
    {
        var isDependency = false;
        recorder.ForOneComponent(componentIdToValidate, grouping =>
        {
            isDependency = true;
            foreach (var predicate in locatingPredicatesForParentExplicitReference)
            {
                var dependencyModel = recorder.GetDetectedComponents().Select(x => x.Component).OfType<TTypedComponent>()
                    .FirstOrDefault(predicate) as TypedComponent;
                isDependency &= grouping.ParentComponentIdsThatAreExplicitReferences.Contains(dependencyModel.Id);
            }
        });

        return isDependency;
    }

    public static void AssertAllExplicitlyReferencedComponents<TTypedComponent>(
        this IComponentRecorder recorder,
        string componentIdToValidate,
        params Func<TTypedComponent, bool>[] locatingPredicatesForParentExplicitReference)
    {
        recorder.ForOneComponent(componentIdToValidate, grouping =>
        {
            var explicitReferrers = new HashSet<string>(grouping.ParentComponentIdsThatAreExplicitReferences);
            var assertionIndex = 0;
            foreach (var predicate in locatingPredicatesForParentExplicitReference)
            {
                if (recorder.GetDetectedComponents().Select(x => x.Component).OfType<TTypedComponent>()
                        .FirstOrDefault(predicate) is not TypedComponent dependencyModel)
                {
                    throw new InvalidOperationException($"One of the predicates (index {assertionIndex}) failed to find a valid component in the Scan Result's discovered components.");
                }

                if (!grouping.ParentComponentIdsThatAreExplicitReferences.Contains(dependencyModel.Id))
                {
                    throw new InvalidOperationException($"Expected component Id {componentIdToValidate} to have {dependencyModel.Id} as a parent explicit reference, but did not.");
                }

                explicitReferrers.Remove(dependencyModel.Id);
                assertionIndex++;
            }

            if (explicitReferrers.Count > 0)
            {
                throw new InvalidOperationException($"Component Id {componentIdToValidate} had parent explicit references ({string.Join(',', explicitReferrers)}) that were not verified via submitted delegates.");
            }
        });
    }

    private static ComponentOrientedGrouping TupleToObject(IEnumerable<(string Location, IDependencyGraph Graph, string ComponentId)> x)
    {
        var additionalRelatedFiles = new List<string>(x.SelectMany(y => y.Graph.GetAdditionalRelatedFiles()));
        additionalRelatedFiles.AddRange(x.Select(y => y.Location));

        return new ComponentOrientedGrouping
        {
            ComponentId = x.First().ComponentId,
            FoundInGraphs = x.Select(y => (y.Location, y.Graph)).ToList(),
            AllFileLocations = additionalRelatedFiles.Distinct().ToList(),
            ParentComponentIdsThatAreExplicitReferences = x.SelectMany(y => y.Graph.GetExplicitReferencedDependencyIds(x.First().ComponentId)).Distinct().ToList(),
        };
    }

    private static List<IGrouping<string, (string Location, IDependencyGraph Graph, string ComponentId)>> GroupByComponentId(IReadOnlyDictionary<string, IDependencyGraph> graphs)
    {
        return graphs
            .Select(x => (Location: x.Key, Graph: x.Value))
            .SelectMany(x => x.Graph.GetComponents()
                .Select(componentId => (x.Location, x.Graph, ComponentId: componentId)))
            .GroupBy(x => x.ComponentId)
            .ToList();
    }

    public static void CheckGraphStructure(IDependencyGraph graph, Dictionary<string, string[]> graphComponentsWithDeps)
    {
        var graphComponents = graph.GetComponents().ToArray();
        graphComponents.Should().HaveCount(
            graphComponentsWithDeps.Keys.Count,
            $"Expected {graphComponentsWithDeps.Keys.Count} component to be recorded but got {graphComponents.Length} instead!");

        foreach (var componentId in graphComponentsWithDeps.Keys)
        {
            graphComponents.Should().Contain(
                componentId, $"Component `{componentId}` not recorded!");

            var recordedDeps = graph.GetDependenciesForComponent(componentId).ToArray();
            var expectedDeps = graphComponentsWithDeps[componentId];

            recordedDeps.Should().HaveCount(
                expectedDeps.Length,
                $"Count missmatch of expected dependencies ({JsonConvert.SerializeObject(expectedDeps)}) and recorded dependencies ({JsonConvert.SerializeObject(recordedDeps)}) for `{componentId}`!");

            foreach (var expectedDep in expectedDeps)
            {
                recordedDeps.Should().Contain(
                    expectedDep, $"Expected `{expectedDep}` in the list of dependencies for `{componentId}` but only recorded: {JsonConvert.SerializeObject(recordedDeps)}");
            }
        }
    }

    public static void CheckChild<T>(IComponentRecorder recorder, string childId, string[] parentIds)
        where T : TypedComponent
    {
        recorder.AssertAllExplicitlyReferencedComponents(
            childId,
            parentIds.Select(parentId => new Func<T, bool>(x => x.Id == parentId)).ToArray());
    }

    public class ComponentOrientedGrouping
    {
        public IEnumerable<(string ManifestFile, IDependencyGraph Graph)> FoundInGraphs { get; set; }

        public string ComponentId { get; set; }

        public IEnumerable<string> AllFileLocations { get; set; }

        public IEnumerable<string> ParentComponentIdsThatAreExplicitReferences { get; internal set; }
    }
}
