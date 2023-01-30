namespace Microsoft.ComponentDetection.Contracts;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>A detected component, found during component detection scans. This is the container for all metadata gathered during detection.</summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class DetectedComponent
{
    private readonly object hashLock = new object();

    /// <summary>Creates a new DetectedComponent.</summary>
    /// <param name="component">The typed component instance to base this detection on.</param>
    /// <param name="detector">The detector that detected this component.</param>
    /// <param name="containerDetailsId">Id of the containerDetails, this is only necessary if the component was found inside a container.</param>
    /// <param name="containerLayerId">Id of the layer the component was found, this is only necessary if the component was found inside a container.</param>
    public DetectedComponent(TypedComponent.TypedComponent component, IComponentDetector detector = null, int? containerDetailsId = null, int? containerLayerId = null)
    {
        this.Component = component;
        this.FilePaths = new HashSet<string>();
        this.DetectedBy = detector;
        this.ContainerDetailIds = new HashSet<int>();
        this.ContainerLayerIds = new Dictionary<int, IEnumerable<int>>();
        if (containerDetailsId.HasValue)
        {
            this.ContainerDetailIds.Add(containerDetailsId.Value);
            if (containerLayerId.HasValue)
            {
                this.ContainerLayerIds.Add(containerDetailsId.Value, new List<int>() { containerLayerId.Value });
            }
        }
    }

    /// <summary>
    /// Gets or sets the detector that detected this component.
    /// </summary>
    public IComponentDetector DetectedBy { get; set; }

    /// <summary>Gets the component associated with this detection.</summary>
    public TypedComponent.TypedComponent Component { get; private set; }

    /// <summary> Gets or sets the hashset containing the file paths associated with the component. </summary>
    public HashSet<string> FilePaths { get; set; }

    /// <summary> Gets or sets the dependency roots for this component. </summary>
    public HashSet<TypedComponent.TypedComponent> DependencyRoots { get; set; }

    /// <summary>Gets or sets the flag to mark the component as a development dependency or not.
    /// This is used at build or development time not a distributed dependency.</summary>
    public bool? DevelopmentDependency { get; set; }

    /// <summary> Gets or sets the details of the container where this component was found.</summary>
    public HashSet<int> ContainerDetailIds { get; set; }

    /// <summary> Gets or sets the layer within a container where this component was found.</summary>
    public IDictionary<int, IEnumerable<int>> ContainerLayerIds { get; set; }

    /// <summary> Gets or sets Dependency Scope of the component.</summary>
    public DependencyScope? DependencyScope { get; set; }

    private string DebuggerDisplay => $"{this.Component.DebuggerDisplay}";

    /// <summary>Adds a filepath to the FilePaths hashset for this detected component.</summary>
    /// <param name="filePath">The file path to add to the hashset.</param>
    public void AddComponentFilePath(string filePath)
    {
        lock (this.hashLock)
        {
            this.FilePaths.Add(filePath);
        }
    }
}
