namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

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
    public ExperimentDiff(
        IEnumerable<ExperimentComponent> controlGroupComponents,
        IEnumerable<ExperimentComponent> experimentGroupComponents)
    {
        var oldComponentDictionary = controlGroupComponents.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var newComponentDictionary = experimentGroupComponents.DistinctBy(x => x.Id).ToDictionary(x => x.Id);

        this.AddedIds = newComponentDictionary.Keys.Except(oldComponentDictionary.Keys).ToImmutableList();
        this.RemovedIds = oldComponentDictionary.Keys.Except(newComponentDictionary.Keys).ToImmutableList();

        var developmentDependencyChanges = new List<DevelopmentDependencyChange>();
        var addedRootIds = new Dictionary<string, IReadOnlySet<string>>();
        var removedRootIds = new Dictionary<string, IReadOnlySet<string>>();

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

        this.DevelopmentDependencyChanges = developmentDependencyChanges.AsReadOnly();
        this.AddedRootIds = addedRootIds.ToImmutableDictionary();
        this.RemovedRootIds = removedRootIds.ToImmutableDictionary();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentDiff"/> class.
    /// </summary>
    /// <param name="addedIds">The added IDs.</param>
    /// <param name="removedIds">The removed IDs.</param>
    /// <param name="developmentDependencyChanges">The development dependency changes.</param>
    /// <param name="addedRootIds">The added root IDs.</param>
    /// <param name="removedRootIds">The removed root IDs.</param>
    public ExperimentDiff(
        IReadOnlyCollection<string> addedIds,
        IReadOnlyCollection<string> removedIds,
        IReadOnlyCollection<DevelopmentDependencyChange> developmentDependencyChanges,
        IReadOnlyDictionary<string, IReadOnlySet<string>> addedRootIds,
        IReadOnlyDictionary<string, IReadOnlySet<string>> removedRootIds)
    {
        this.AddedIds = addedIds;
        this.RemovedIds = removedIds;
        this.DevelopmentDependencyChanges = developmentDependencyChanges;
        this.AddedRootIds = addedRootIds;
        this.RemovedRootIds = removedRootIds;
    }

    /// <summary>
    /// Gets a list of component IDs that were present in the experimental group but not the control group.
    /// </summary>
    public IReadOnlyCollection<string> AddedIds { get; }

    /// <summary>
    /// Gets a list of component IDs that were present in the control group but not the experimental group.
    /// </summary>
    public IReadOnlyCollection<string> RemovedIds { get; }

    /// <summary>
    /// Gets a list of changes to the development dependency status of components.
    /// </summary>
    public IReadOnlyCollection<DevelopmentDependencyChange> DevelopmentDependencyChanges { get; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were added to the component. The component ID
    /// is the key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> AddedRootIds { get; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were removed from the component. The component
    /// ID is the key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> RemovedRootIds { get; }

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
}
