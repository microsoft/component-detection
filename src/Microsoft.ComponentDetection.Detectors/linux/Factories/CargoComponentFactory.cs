#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="CargoComponent"/> instances from Rust crate artifacts.
/// </summary>
public class CargoComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.Cargo;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["rust-crate"];

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

        // Syft provides the source in metadata.Source
        var source = artifact.Metadata?.Source?.String;

        return new CargoComponent(
            name: artifact.Name,
            version: artifact.Version,
            author: author,
            license: license,
            source: source
        );
    }
}
