namespace Microsoft.ComponentDetection.Detectors.Linux.Filters;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Interface for filtering or transforming Syft artifacts before component creation.
/// Useful for handling distribution-specific workarounds or edge cases.
/// </summary>
public interface IArtifactFilter
{
    /// <summary>
    /// Filters the provided artifacts and returns the filtered collection.
    /// </summary>
    /// <param name="artifacts">The artifacts to filter.</param>
    /// <param name="distro">The distribution information from Syft output.</param>
    /// <returns>The filtered collection of artifacts.</returns>
    public IEnumerable<ArtifactElement> Filter(IEnumerable<ArtifactElement> artifacts, Distro distro);
}
