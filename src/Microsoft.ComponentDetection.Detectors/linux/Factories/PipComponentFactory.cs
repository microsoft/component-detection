#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="PipComponent"/> instances from Python package artifacts.
/// </summary>
public class PipComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["python"];

    /// <inheritdoc/>
    public override TypedComponent CreateComponent(ArtifactElement artifact, Distro distro)
    {
        if (artifact == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(artifact.Name) || string.IsNullOrWhiteSpace(artifact.Version))
        {
            return null;
        }

        var author = GetAuthorFromArtifact(artifact);
        var license = GetLicenseFromArtifact(artifact);

        return new PipComponent(
            name: artifact.Name,
            version: artifact.Version,
            author: author,
            license: license);
    }
}
