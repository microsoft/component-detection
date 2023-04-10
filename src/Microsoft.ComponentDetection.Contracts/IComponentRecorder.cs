namespace Microsoft.ComponentDetection.Contracts;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

public interface IComponentRecorder
{
    TypedComponent.TypedComponent GetComponent(string componentId);

    IEnumerable<DetectedComponent> GetDetectedComponents();

    IEnumerable<string> GetSkippedComponents();

    ISingleFileComponentRecorder CreateSingleFileComponentRecorder(string location);

    IReadOnlyDictionary<string, IDependencyGraph> GetDependencyGraphsByLocation();
}

public interface ISingleFileComponentRecorder
{
    string ManifestFileLocation { get; }

    IDependencyGraph DependencyGraph { get; }

    /// <summary>
    /// Add or Update a component. In case that a parent componentId is specified
    /// an edge is created between those components in the dependency graph.
    /// </summary>
    /// <param name="detectedComponent">Component to add.</param>
    /// <param name="isExplicitReferencedDependency">The value define if the component was referenced manually by the user in the location where the scanning is taking place.</param>
    /// <param name="parentComponentId">Id of the parent component.</param>
    /// <param name="isDevelopmentDependency">Boolean value indicating whether or not a component is a development-time dependency. Null implies that the value is unknown.</param>
    /// <param name="dependencyScope">Enum value indicating scope of the component. </param>
    void RegisterUsage(
        DetectedComponent detectedComponent,
        bool isExplicitReferencedDependency = false,
        string parentComponentId = null,
        bool? isDevelopmentDependency = null,
        DependencyScope? dependencyScope = null);

    /// <summary>
    /// Register that a package was unable to be processed.
    /// </summary>
    /// <param name="skippedComponent">Component version identifier.</param>
    void RegisterPackageParseFailure(string skippedComponent);

    DetectedComponent GetComponent(string componentId);

    /// <summary>
    /// Any file added here will be reported as a location on ALL components found in current graph.
    /// </summary>
    void AddAdditionalRelatedFile(string relatedFilePath);

    IReadOnlyDictionary<string, DetectedComponent> GetDetectedComponents();

    IComponentRecorder GetParentComponentRecorder();
}

public interface IDependencyGraph
{
    /// <summary>
    /// Gets the componentIds that are dependencies for a given componentId.
    /// </summary>
    /// <param name="componentId">The component id to look up dependencies for.</param>
    /// <returns>The componentIds that are dependencies for a given componentId.</returns>
    IEnumerable<string> GetDependenciesForComponent(string componentId);

    /// <summary>
    /// Gets all componentIds that are in the dependency graph.
    /// </summary>
    /// <returns>The componentIds that are part of the dependency graph.</returns>
    IEnumerable<string> GetComponents();

    /// <summary>
    /// Returns true if a componentId is an explicitly referenced dependency.
    /// </summary>
    /// <param name="componentId">The componentId to check.</param>
    /// <returns>True if explicitly referenced, false otherwise.</returns>
    bool IsComponentExplicitlyReferenced(string componentId);

    HashSet<string> GetAdditionalRelatedFiles();

    /// <summary>
    /// Returns true if a componentId is registered in the graph.
    /// </summary>
    /// <param name="componentId">The componentId to check.</param>
    /// <returns>True if registered in the graph, false otherwise.</returns>
    bool Contains(string componentId);

    /// <summary>
    /// Returns true if a componentId is a development dependency, and false if it is not.
    /// Null can be returned if a detector doesn't have confidence one way or the other.
    /// </summary>
    /// <param name="componentId">The componentId to check.</param>
    /// <returns>True if a development dependency, false if not. Null when unknown.</returns>
    bool? IsDevelopmentDependency(string componentId);

    /// <summary>
    /// Returns DepedencyScope for the given componentId.
    /// Null can be returned if a detector doesn't have the scope infromation.
    /// </summary>
    /// <param name="componentId">The componentId to check.</param>
    /// <returns> DependencyScope <see cref="DependencyScope"/> for the given componentId. </returns>
    DependencyScope? GetDependencyScope(string componentId);

    /// <summary>
    /// Gets the component IDs of all explicitly referenced components.
    /// </summary>
    /// <returns>An enumerable of the component IDs of all explicilty referenced components.</returns>
    IEnumerable<string> GetAllExplicitlyReferencedComponents();

    /// <summary>
    /// Returns the set of component ids that are explicit references to the given component id.
    /// </summary>
    /// <param name="componentId">The leaf level component to find explicit references for.</param>
    /// <returns>A  collection fo all explicit references to the given component.</returns>
    ICollection<string> GetExplicitReferencedDependencyIds(string componentId);

    /// <summary>
    /// Gets the componentIds that are ancestors for a given componentId.
    /// </summary>
    /// <param name="componentId">The component id to look up ancestors for.</param>
    /// <returns>The componentIds that are ancestors for a given componentId.</returns>
    ICollection<string> GetAncestors(string componentId);
}
