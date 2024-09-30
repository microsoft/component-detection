namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// A model for the difference between two sets of <see cref="ExperimentComponent"/> instances.
/// </summary>
public class ExperimentDiff
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentDiff"/> class.
    /// </summary>
    /// <param name="controlGroupComponents">A set of components from the control group.</param>
    /// <param name="experimentGroupComponents">A set of components from the experimental group.</param>
    /// <param name="controlDetectors">The set of control detectors.</param>
    /// <param name="experimentalDetectors">The set of experimental detectors.</param>
    /// <param name="additionalProperties">The set of additional metrics to be captured.</param>
    public ExperimentDiff(
        IEnumerable<ExperimentComponent> controlGroupComponents,
        IEnumerable<ExperimentComponent> experimentGroupComponents,
        IEnumerable<(string DetectorId, TimeSpan DetectorRunTime)> controlDetectors = null,
        IEnumerable<(string DetectorId, TimeSpan DetectorRunTime)> experimentalDetectors = null,
        IEnumerable<(string PropertyKey, string PropertyValue)> additionalProperties = null)
    {
        var oldComponentDictionary = controlGroupComponents.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var newComponentDictionary = experimentGroupComponents.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        additionalProperties ??= [];
        this.AdditionalProperties = additionalProperties?.Select(kv => new KeyValuePair<string, string>(kv.PropertyKey, kv.PropertyValue)).ToImmutableList();

        this.AddedIds = newComponentDictionary.Keys.Except(oldComponentDictionary.Keys).ToImmutableList();
        this.RemovedIds = oldComponentDictionary.Keys.Except(newComponentDictionary.Keys).ToImmutableList();

        var developmentDependencyChanges = new List<DevelopmentDependencyChange>();
        var addedRootIds = new Dictionary<string, IReadOnlySet<string>>();
        var removedRootIds = new Dictionary<string, IReadOnlySet<string>>();
        var controlDetectorList = new List<ExperimentDetector>();
        var experimentDetectorList = new List<ExperimentDetector>();

        // Need performance benchmark to see if this is worth parallelization
        foreach (var id in newComponentDictionary.Keys.Intersect(oldComponentDictionary.Keys))
        {
            var oldComponent = oldComponentDictionary[id];
            var newComponent = newComponentDictionary[id];

            if (oldComponent.DevelopmentDependency != newComponent.DevelopmentDependency)
            {
                developmentDependencyChanges.Add(new DevelopmentDependencyChange(
                    id,
                    oldComponent.DevelopmentDependency,
                    newComponent.DevelopmentDependency));
            }

            var newRoots = newComponent.RootIds.Except(oldComponent.RootIds).ToImmutableHashSet();
            var removedRoots = oldComponent.RootIds.Except(newComponent.RootIds).ToImmutableHashSet();

            if (newRoots.Count > 0)
            {
                addedRootIds[id] = newRoots;
            }

            if (removedRoots.Count > 0)
            {
                removedRootIds[id] = removedRoots;
            }
        }

        if (controlDetectors != null)
        {
            foreach (var (detectorId, detectorRunTime) in controlDetectors)
            {
                controlDetectorList.Add(new ExperimentDetector(detectorId, detectorRunTime));
            }

            this.ControlDetectors = controlDetectorList.ToImmutableList();
        }

        if (experimentalDetectors != null)
        {
            foreach (var (detectorId, detectorRunTime) in experimentalDetectors)
            {
                experimentDetectorList.Add(new ExperimentDetector(detectorId, detectorRunTime));
            }

            this.ExperimentalDetectors = experimentDetectorList.ToImmutableList();
        }

        this.DevelopmentDependencyChanges = developmentDependencyChanges.AsReadOnly();
        this.AddedRootIds = addedRootIds.ToImmutableDictionary();
        this.RemovedRootIds = removedRootIds.ToImmutableDictionary();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentDiff"/> class.
    /// </summary>
    public ExperimentDiff()
    {
    }

    /// <summary>
    /// Gets a list of component IDs that were present in the experimental group but not the control group.
    /// </summary>
    public IReadOnlyCollection<string> AddedIds { get; init; }

    /// <summary>
    /// Detector Ids of the control group.
    /// </summary>
    public IReadOnlyCollection<ExperimentDetector> ControlDetectors { get; set; }

    /// <summary>
    /// Detector Ids of the experiment group.
    /// </summary>
    public IReadOnlyCollection<ExperimentDetector> ExperimentalDetectors { get; set; }

    /// <summary>
    /// Gets a list of component IDs that were present in the control group but not the experimental group.
    /// </summary>
    public IReadOnlyCollection<string> RemovedIds { get; init; }

    /// <summary>
    /// Gets a list of changes to the development dependency status of components.
    /// </summary>
    public IReadOnlyCollection<DevelopmentDependencyChange> DevelopmentDependencyChanges { get; init; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were added to the component. The component ID
    /// is the key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> AddedRootIds { get; init; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were removed from the component. The component
    /// ID is the key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> RemovedRootIds { get; init; }

    /// <summary>
    /// Any additional metrics that were captured for the experiment.
    /// </summary>
    public IReadOnlyCollection<KeyValuePair<string, string>> AdditionalProperties { get; init; }

    /// <summary>
    /// Stores information about a change to the development dependency status of a component.
    /// </summary>
    public class DevelopmentDependencyChange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DevelopmentDependencyChange"/> class.
        /// </summary>
        /// <param name="id">The component ID.</param>
        /// <param name="oldValue">The old value of the development dependency status.</param>
        /// <param name="newValue">The new value of the development dependency status.</param>
        public DevelopmentDependencyChange(string id, bool oldValue, bool newValue)
        {
            this.Id = id;
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>
        /// Gets the component ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the old value of the development dependency status.
        /// </summary>
        public bool OldValue { get; }

        /// <summary>
        /// Gets the new value of the development dependency status.
        /// </summary>
        public bool NewValue { get; }
    }

    /// <summary>
    /// Stores information about a detector run.
    /// </summary>
    public class ExperimentDetector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentDetector"/> class.
        /// </summary>
        /// <param name="detectorId">Id of the detector.</param>
        /// <param name="detectorRunTime">Run time of the detector.</param>
        public ExperimentDetector(string detectorId, TimeSpan detectorRunTime)
        {
            this.DetectorId = detectorId;
            this.DetectorRunTime = detectorRunTime;
        }

        /// <summary>
        /// Gets the detector Id.
        /// </summary>
        public string DetectorId { get; set; }

        /// <summary>
        /// Gets the detector run time.
        /// </summary>
        public TimeSpan DetectorRunTime { get; set; }
    }
}
