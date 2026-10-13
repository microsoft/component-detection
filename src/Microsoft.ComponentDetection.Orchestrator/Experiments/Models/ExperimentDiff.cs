#nullable disable
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
        var locationChanges = new Dictionary<string, LocationChange>();
        var controlDetectorList = new List<ExperimentDetector>();
        var experimentDetectorList = new List<ExperimentDetector>();

        // Need performance benchmark to see if this is worth parallelization
        foreach (var newComponentPair in newComponentDictionary)
        {
            var newComponent = newComponentPair.Value;
            var id = newComponentPair.Key;

            if (!oldComponentDictionary.TryGetValue(id, out var oldComponent))
            {
                if (newComponent.DevelopmentDependency)
                {
                    developmentDependencyChanges.Add(new DevelopmentDependencyChange(
                        id,
                        oldValue: false,
                        newValue: newComponent.DevelopmentDependency));
                }

                // Track locations for newly added components
                if (newComponent.Locations.Count > 0)
                {
                    // Pass HashSet directly - no conversion needed
                    locationChanges[id] = new LocationChange(
                        id,
                        controlLocations: [],
                        experimentLocations: newComponent.Locations);
                }

                continue;
            }

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

            // Track location changes if counts differ or either set has locations
            // Note: We don't check SetEquals() here as it's O(n) and expensive for large sets.
            // The LocationChange uses lazy evaluation, so no diff is computed unless accessed.
            if (oldComponent.Locations.Count != newComponent.Locations.Count)
            {
                // Pass HashSets directly - LocationChange will compute diffs lazily if needed
                locationChanges[id] = new LocationChange(
                    id,
                    controlLocations: oldComponent.Locations,
                    experimentLocations: newComponent.Locations);
            }
        }

        // Track locations for removed components
        foreach (var oldComponentPair in oldComponentDictionary)
        {
            var id = oldComponentPair.Key;
            if (!newComponentDictionary.ContainsKey(id) && oldComponentPair.Value.Locations.Count > 0)
            {
                // Pass HashSet directly - no conversion needed
                locationChanges[id] = new LocationChange(
                    id,
                    controlLocations: oldComponentPair.Value.Locations,
                    experimentLocations: []);
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
        this.LocationChanges = locationChanges.ToImmutableDictionary();
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
    /// Gets a dictionary of component IDs to the location changes for that component.
    /// </summary>
    public IReadOnlyDictionary<string, LocationChange> LocationChanges { get; init; }

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
    /// Stores information about changes to the file path locations where a component was found.
    /// </summary>
    public class LocationChange
    {
        private IReadOnlySet<string> addedLocations;
        private IReadOnlySet<string> removedLocations;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocationChange"/> class.
        /// </summary>
        /// <param name="componentId">The component ID.</param>
        /// <param name="controlLocations">The locations found by the control detector.</param>
        /// <param name="experimentLocations">The locations found by the experimental detector.</param>
        public LocationChange(
            string componentId,
            IEnumerable<string> controlLocations,
            IEnumerable<string> experimentLocations)
        {
            this.ComponentId = componentId;

            // Store as HashSet if not already, or keep the reference if it's already a HashSet
            this.ControlLocations = controlLocations as IReadOnlySet<string> ?? controlLocations.ToHashSet();
            this.ExperimentLocations = experimentLocations as IReadOnlySet<string> ?? experimentLocations.ToHashSet();

            this.ControlLocationCount = this.ControlLocations.Count;
            this.ExperimentLocationCount = this.ExperimentLocations.Count;
            this.LocationCountDelta = this.ExperimentLocationCount - this.ControlLocationCount;
        }

        /// <summary>
        /// Gets the component ID.
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// Gets the locations found by the control detector.
        /// </summary>
        public IReadOnlySet<string> ControlLocations { get; }

        /// <summary>
        /// Gets the locations found by the experimental detector.
        /// </summary>
        public IReadOnlySet<string> ExperimentLocations { get; }

        /// <summary>
        /// Gets the locations found by the experimental detector but not the control detector.
        /// Computed lazily to avoid allocations if not accessed.
        /// </summary>
        public IReadOnlySet<string> AddedLocations
        {
            get
            {
                this.addedLocations ??= this.ExperimentLocations.Except(this.ControlLocations).ToHashSet();
                return this.addedLocations;
            }
        }

        /// <summary>
        /// Gets the locations found by the control detector but not the experimental detector.
        /// Computed lazily to avoid allocations if not accessed.
        /// </summary>
        public IReadOnlySet<string> RemovedLocations
        {
            get
            {
                this.removedLocations ??= this.ControlLocations.Except(this.ExperimentLocations).ToHashSet();
                return this.removedLocations;
            }
        }

        /// <summary>
        /// Gets the number of locations found by the control detector.
        /// </summary>
        public int ControlLocationCount { get; }

        /// <summary>
        /// Gets the number of locations found by the experimental detector.
        /// </summary>
        public int ExperimentLocationCount { get; }

        /// <summary>
        /// Gets the difference in location count (experiment - control).
        /// A positive value means the experiment found more locations.
        /// A negative value means the experiment found fewer locations.
        /// </summary>
        public int LocationCountDelta { get; }
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
