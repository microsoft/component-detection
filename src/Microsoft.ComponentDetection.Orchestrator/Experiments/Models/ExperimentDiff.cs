namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A model for the difference between two sets of <see cref="ExperimentComponent"/> instances.
/// </summary>
public class ExperimentDiff
{
    /// <summary>
    /// Creates a new <see cref="ExperimentDiff"/>.
    /// </summary>
    /// <param name="controlGroupComponents">A set of components from the control group.</param>
    /// <param name="experimentGroupComponents">A set of components from the experimental group.</param>
    public ExperimentDiff(
        IEnumerable<ExperimentComponent> controlGroupComponents,
        IEnumerable<ExperimentComponent> experimentGroupComponents)
    {
        var oldComponentDictionary = controlGroupComponents.ToDictionary(x => x.Id);
        var newComponentDictionary = experimentGroupComponents.ToDictionary(x => x.Id);

        this.AddedIds = newComponentDictionary.Keys.Except(oldComponentDictionary.Keys).ToList();
        this.RemovedIds = oldComponentDictionary.Keys.Except(newComponentDictionary.Keys).ToList();

        this.DevelopmentDependencyChanges = new List<DevelopmentDependencyChange>();
        this.AddedRootIds = new Dictionary<string, HashSet<string>>();
        this.RemovedRootIds = new Dictionary<string, HashSet<string>>();

        // Need performance benchmark to see if this is worth parallelization
        foreach (var id in newComponentDictionary.Keys.Intersect(oldComponentDictionary.Keys))
        {
            var oldComponent = oldComponentDictionary[id];
            var newComponent = newComponentDictionary[id];

            if (oldComponent.DevelopmentDependency != newComponent.DevelopmentDependency)
            {
                this.DevelopmentDependencyChanges.Add(new DevelopmentDependencyChange(
                    id,
                    oldComponent.DevelopmentDependency,
                    newComponent.DevelopmentDependency));
            }

            var addedRootIds = newComponent.RootIds.Except(oldComponent.RootIds).ToHashSet();
            var removedRootIds = oldComponent.RootIds.Except(newComponent.RootIds).ToHashSet();

            if (addedRootIds.Count > 0)
            {
                this.AddedRootIds[id] = addedRootIds;
            }

            if (removedRootIds.Count > 0)
            {
                this.RemovedRootIds[id] = removedRootIds;
            }
        }
    }

    /// <summary>
    /// Gets a list of component IDs that were present in the experimental group but not the control group.
    /// </summary>
    public List<string> AddedIds { get; }

    /// <summary>
    /// Gets a list of component IDs that were present in the control group but not the experimental group.
    /// </summary>
    public List<string> RemovedIds { get; }

    /// <summary>
    /// Gets a list of changes to the development dependency status of components.
    /// </summary>
    public List<DevelopmentDependencyChange> DevelopmentDependencyChanges { get; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were added to the component. The component ID
    /// is the key.
    /// </summary>
    public Dictionary<string, HashSet<string>> AddedRootIds { get; }

    /// <summary>
    /// Gets a dictionary of component IDs to the set of root IDs that were removed from the component. The component
    /// ID is the key.
    /// </summary>
    public Dictionary<string, HashSet<string>> RemovedRootIds { get; }

    /// <summary>
    /// Stores information about a change to the development dependency status of a component.
    /// </summary>
    public class DevelopmentDependencyChange
    {
        /// <summary>
        /// Creates a new <see cref="DevelopmentDependencyChange"/>.
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
