using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Yarn
{
    public class YarnEntry
    {
        public string LookupKey => $"{Name}@{Version}";

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
        public IList<string> Satisfied { get; } = new List<string>();

        /// <summary>
        /// Gets the name@version dependencies that this package requires.
        /// </summary>
        public IList<YarnDependency> Dependencies { get; } = new List<YarnDependency>();

        /// <summary>
        /// Gets the name@version dependencies that this package requires.
        /// </summary>
        public IList<YarnDependency> OptionalDependencies { get; } = new List<YarnDependency>();

        /// <summary>
        /// Gets or sets a value indicating whether or not the component is a dev dependency.
        /// </summary>
        public bool DevDependency { get; set; }
    }
}
