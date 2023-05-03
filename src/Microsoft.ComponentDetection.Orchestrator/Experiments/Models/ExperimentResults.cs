namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Stores the results of a detector execution for an experiment. Buckets components into a control group and an
/// experimental group.
/// </summary>
public class ExperimentResults
{
    private static readonly IEqualityComparer<ExperimentComponent> Comparer = new ExperimentComponentComparer();

    private readonly ConcurrentDictionary<ExperimentComponent, byte> controlGroupComponents = new(Comparer);

    private readonly ConcurrentDictionary<ExperimentComponent, byte> experimentGroupComponents = new(Comparer);

    /// <summary>
    /// The set of components in the control group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ControlGroupComponents =>
        this.controlGroupComponents.Keys.ToImmutableHashSet();

    /// <summary>
    /// The set of components in the experimental group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ExperimentGroupComponents =>
        this.experimentGroupComponents.Keys.ToImmutableHashSet();

    /// <summary>
    /// Adds the components to the control group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToControlGroup(IEnumerable<DetectedComponent> components) =>
        AddComponents(this.controlGroupComponents, components);

    /// <summary>
    /// Adds the components to the experimental group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToExperimentalGroup(IEnumerable<DetectedComponent> components) =>
        AddComponents(this.experimentGroupComponents, components);

    private static void AddComponents(ConcurrentDictionary<ExperimentComponent, byte> group, IEnumerable<DetectedComponent> components)
    {
        foreach (var experimentComponent in components.Select(x => new ExperimentComponent(x)))
        {
            _ = group.TryAdd(experimentComponent, 0);
        }
    }
}
