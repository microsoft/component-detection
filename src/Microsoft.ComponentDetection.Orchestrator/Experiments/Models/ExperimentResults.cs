namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

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
    private static readonly IEqualityComparer<ExperimentComponent> ComponentComparer = new ExperimentComponentComparer();

    private readonly HashSet<ExperimentComponent> controlGroupComponents = new(ComponentComparer);

    private readonly HashSet<ExperimentComponent> experimentGroupComponents = new(ComponentComparer);

    /// <summary>
    /// The set of components in the control group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ControlGroupComponents =>
        this.controlGroupComponents.ToImmutableHashSet();

    /// <summary>
    /// The set of components in the experimental group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ExperimentGroupComponents =>
        this.experimentGroupComponents.ToImmutableHashSet();

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

    private static void AddComponents(ISet<ExperimentComponent> group, IEnumerable<DetectedComponent> components)
    {
        foreach (var experimentComponent in components.Select(x => new ExperimentComponent(x)))
        {
            group.Add(experimentComponent);
        }
    }
}
