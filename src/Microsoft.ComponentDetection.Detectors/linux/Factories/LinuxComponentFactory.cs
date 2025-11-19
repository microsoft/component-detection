#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="LinuxComponent"/> instances from system package artifacts (apk, deb, rpm).
/// </summary>
public class LinuxComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["apk", "deb", "rpm"];

    /// <inheritdoc/>
    public override TypedComponent CreateComponent(ArtifactElement artifact, Distro distro)
    {
        if (artifact == null || distro == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(artifact.Name) || string.IsNullOrWhiteSpace(artifact.Version))
        {
            return null;
        }

        var license = GetLicenseFromArtifact(artifact);
        var supplier = GetAuthorFromArtifact(artifact);

        return new LinuxComponent(
            distribution: distro.Id,
            release: distro.VersionId,
            name: artifact.Name,
            version: artifact.Version,
            license: license,
            author: supplier);
    }
}
