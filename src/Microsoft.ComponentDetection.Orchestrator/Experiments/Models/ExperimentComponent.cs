#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
/// A model representing a component detected by a detector, as relevant to an experiment.
/// </summary>
public record ExperimentComponent
{
    /// <summary>
    /// Creates a new <see cref="ExperimentComponent"/> from a <see cref="ScannedComponent"/>.
    /// </summary>
    /// <param name="detectedComponent">The detected component.</param>
    public ExperimentComponent(ScannedComponent detectedComponent)
    {
        this.Id = detectedComponent.Component.Id;
        this.DevelopmentDependency = detectedComponent.IsDevelopmentDependency ?? false;
        this.RootIds = detectedComponent.TopLevelReferrers?.Select(x => x.Id).ToHashSet() ?? [];
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
