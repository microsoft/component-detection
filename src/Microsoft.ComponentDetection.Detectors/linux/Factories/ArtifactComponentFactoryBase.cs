#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Abstract base class for artifact component factories that provides common functionality
/// for extracting license and author information from Syft artifacts.
/// </summary>
public abstract class ArtifactComponentFactoryBase : IArtifactComponentFactory
{
    /// <inheritdoc/>
    public abstract IEnumerable<string> SupportedArtifactTypes { get; }

    /// <inheritdoc/>
    public abstract TypedComponent CreateComponent(ArtifactElement artifact, Distro distro);

    /// <summary>
    /// Extracts license information from the artifact, checking both metadata and top-level licenses array.
    /// </summary>
    /// <param name="artifact">The artifact element from Syft output.</param>
    /// <returns>A comma-separated string of license values, or null if no licenses are found.</returns>
    protected static string GetLicenseFromArtifact(ArtifactElement artifact)
    {
        // First try metadata.License which may be a string
        var license = artifact.Metadata?.License?.String;
        if (license != null)
        {
            return license;
        }

        // Fall back to top-level Licenses array
        var licenses = artifact.Licenses;
        if (licenses != null && licenses.Length != 0)
        {
            return string.Join(", ", licenses.Select(l => l.Value));
        }

        return null;
    }

    /// <summary>
    /// Extracts author information from the artifact metadata, checking both Author and Maintainer fields.
    /// </summary>
    /// <param name="artifact">The artifact element from Syft output.</param>
    /// <returns>The author or maintainer string, or null if neither is found.</returns>
    protected static string GetAuthorFromArtifact(ArtifactElement artifact)
    {
        var author = artifact.Metadata?.Author;
        if (!string.IsNullOrEmpty(author))
        {
            return author;
        }

        var maintainer = artifact.Metadata?.Maintainer;
        if (!string.IsNullOrEmpty(maintainer))
        {
            return maintainer;
        }

        return null;
    }
}
