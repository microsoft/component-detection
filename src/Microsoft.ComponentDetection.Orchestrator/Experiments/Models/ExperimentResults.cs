namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System;
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
    private readonly object setLock = new();

    private readonly HashSet<ExperimentComponent> controlGroupComponents = new();

    private readonly HashSet<ExperimentComponent> experimentGroupComponents = new();

    /// <summary>
    /// The set of components in the control group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ControlGroupComponents =>
        this.AcquireLockAndRun(() => this.controlGroupComponents.ToImmutableHashSet());

    /// <summary>
    /// The set of components in the experimental group.
    /// </summary>
    public IImmutableSet<ExperimentComponent> ExperimentGroupComponents =>
        this.AcquireLockAndRun(() => this.experimentGroupComponents.ToImmutableHashSet());

    /// <summary>
    /// Adds the components to the control group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToControlGroup(IEnumerable<DetectedComponent> components) =>
        this.AcquireLockAndRun(() => this.AddComponents(this.controlGroupComponents, components));

    /// <summary>
    /// Adds the components to the experimental group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToExperimentalGroup(IEnumerable<DetectedComponent> components) =>
        this.AcquireLockAndRun(() => this.AddComponents(this.experimentGroupComponents, components));

    private void AddComponents(ISet<ExperimentComponent> group, IEnumerable<DetectedComponent> components) =>
        this.AcquireLockAndRun(() =>
        {
            foreach (var experimentComponent in components.Select(x => new ExperimentComponent(x)))
            {
                group.Add(experimentComponent);
            }
        });

    private T AcquireLockAndRun<T>(Func<T> action)
    {
        lock (this.setLock)
        {
            return action();
        }
    }

    private void AcquireLockAndRun(Action action)
    {
        lock (this.setLock)
        {
            action();
        }
    }
}
