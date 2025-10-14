#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>Used to communicate Dependency Scope of Component.
/// Currently only populated for Maven component.
/// The values are ordered in terms of priority, which is used to resolve the scope for duplicate component while merging them.
/// </summary>
public enum DependencyScope
{
    /// <summary>default scope. dependencies are available in the project during all build tasks. propogated to dependent projects. </summary>
    MavenCompile = 0,

    /// <summary> Required at Runtime, but not at compile time.</summary>
    MavenRuntime = 1,

    /// <summary>Dependencies are available only at compile time and in the test classpath of the project. These dependencies are also not transitive.</summary>
    MavenProvided = 2,

    /// <summary>Similar to provided scope. Requires explicit reference to Jar. </summary>
    MavenSystem = 3,

    /// <summary>Used only at runtime.</summary>
    MavenTest = 4,
}
