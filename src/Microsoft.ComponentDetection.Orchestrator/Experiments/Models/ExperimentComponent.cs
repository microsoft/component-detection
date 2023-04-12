namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// A model representing a component detected by a detector, as relevant to an experiment.
/// </summary>
public record ExperimentComponent
{
    /// <summary>
    /// Creates a new <see cref="ExperimentComponent"/> from a <see cref="DetectedComponent"/>.
    /// </summary>
    /// <param name="detectedComponent">The detected component.</param>
    public ExperimentComponent(DetectedComponent detectedComponent)
    {
        this.Id = detectedComponent.Component.Id;
        this.DevelopmentDependency = detectedComponent.DevelopmentDependency ?? false;
        this.RootIds = detectedComponent.DependencyRoots?.Select(x => x.Id).ToHashSet() ?? new HashSet<string>();
    }

    /// <summary>
    /// The component ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// <c>true</c> if the component is a development dependency; otherwise, <c>false</c>.
    /// </summary>
    public bool DevelopmentDependency { get; }

    /// <summary>
    /// The set of root component IDs for this component.
    /// </summary>
    public HashSet<string> RootIds { get; }
}
