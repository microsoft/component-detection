#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.Collections.Generic;

public class YarnEntry
{
    public string LookupKey => $"{this.Name}@{this.Version}";

    /// <summary>
    /// Gets or sets the non-version qualified name of the entry.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the version string of the entry.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the resolution string of the entry.
    /// </summary>
    public string Resolved { get; set; }

    /// <summary>
    /// Gets the satisfied version strings of this entry.
    /// </summary>
    public IList<string> Satisfied { get; } = [];

    /// <summary>
    /// Gets the name@version dependencies that this package requires.
    /// </summary>
    public IList<YarnDependency> Dependencies { get; } = [];

    /// <summary>
    /// Gets the name@version dependencies that this package requires.
    /// </summary>
    public IList<YarnDependency> OptionalDependencies { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether or not the component is a dev dependency.
    /// </summary>
    public bool DevDependency { get; set; }

    /// <summary>
    /// Gets or Sets the location for this yarnentry. Often a file path if not in test circumstances.
    /// </summary>
    public string Location { get; set; }
}
