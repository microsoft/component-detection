#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
/// Stores the results of a detector execution for an experiment. Buckets components into a control group and an
/// experimental group.
/// </summary>
public class ExperimentResults
{
    private static readonly IEqualityComparer<ExperimentComponent> Comparer = new ExperimentComponentComparer();

    private readonly ConcurrentDictionary<ExperimentComponent, byte> controlGroupComponents = new(Comparer);

    private readonly ConcurrentDictionary<ExperimentComponent, byte> experimentGroupComponents = new(Comparer);

    private readonly ConcurrentDictionary<string, TimeSpan> controlDetectors = new();

    private readonly ConcurrentDictionary<string, TimeSpan> experimentalDetectors = new();

    private readonly ConcurrentBag<(string, string)> additionalProperties = [];

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
    /// The set of control detectors.
    /// </summary>
    public IImmutableSet<(string DetectorId, TimeSpan DetectorRunTime)> ControlDetectors =>
        this.controlDetectors.Select(x => (x.Key, x.Value)).ToImmutableHashSet();

    /// <summary>
    /// The set of experimental detectors.
    /// </summary>
    public IImmutableSet<(string DetectorId, TimeSpan DetectorRunTime)> ExperimentalDetectors =>
        this.experimentalDetectors.Select(x => (x.Key, x.Value)).ToImmutableHashSet();

    /// <summary>
    /// The set of experimental detectors.
    /// </summary>
    public IImmutableSet<(string PropertyKey, string PropertyValue)> AdditionalProperties =>
        this.additionalProperties.ToImmutableHashSet();

    /// <summary>
    /// Adds the components to the control group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToControlGroup(IEnumerable<ScannedComponent> components) =>
        AddComponents(this.controlGroupComponents, components);

    /// <summary>
    /// Adds the control detector run times to the experiment telemetry.
    /// </summary>
    /// <param name="controlDetectorId">Id of the control Detector.</param>
    /// <param name="controlDetectorRunTime">Run time of the control detector.</param>
    public void AddControlDetectorTime(string controlDetectorId, TimeSpan controlDetectorRunTime) =>
        AddRunTime(this.controlDetectors, controlDetectorId, controlDetectorRunTime);

    /// <summary>
    /// Adds the experimental detector run times to the experiment telemetry.
    /// </summary>
    /// <param name="experimentalDetectorId">Id of the experimental Detector.</param>
    /// <param name="experimentalDetectorRunTime">Run time of the experimental detector.</param>
    public void AddExperimentalDetectorTime(string experimentalDetectorId, TimeSpan experimentalDetectorRunTime) =>
        AddRunTime(this.experimentalDetectors, experimentalDetectorId, experimentalDetectorRunTime);

    /// <summary>
    /// Adds the components to the experimental group.
    /// </summary>
    /// <param name="components">The components.</param>
    public void AddComponentsToExperimentalGroup(IEnumerable<ScannedComponent> components) =>
        AddComponents(this.experimentGroupComponents, components);

    /// <summary>
    /// Adds a custom metric to the experiment.
    /// </summary>
    /// <param name="properties">list of (key, value) tuples to be captured as additionalProperties. </param>
    public void AddAdditionalPropertiesToExperiment(IEnumerable<(string PropertyKey, string PropertyValue)> properties)
    {
        foreach (var (propertyKey, propertyValue) in properties)
        {
            AddAdditionalProperty(this.additionalProperties, propertyKey, propertyValue);
        }
    }

    private static void AddComponents(ConcurrentDictionary<ExperimentComponent, byte> group, IEnumerable<ScannedComponent> components)
    {
        foreach (var experimentComponent in components.Select(x => new ExperimentComponent(x)))
        {
            _ = group.TryAdd(experimentComponent, 0);
        }
    }

    private static void AddRunTime(ConcurrentDictionary<string, TimeSpan> group, string detectorId, TimeSpan runTime)
    {
        _ = group.TryAdd(detectorId, runTime);
    }

    private static void AddAdditionalProperty(ConcurrentBag<(string PropertyKey, string PropertyValue)> group, string propertyKey, string propertyValue)
    {
        group.Add((propertyKey, propertyValue));
    }
}
