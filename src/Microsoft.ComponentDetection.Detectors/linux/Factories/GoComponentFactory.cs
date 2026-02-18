#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="GoComponent"/> instances from Go module artifacts.
/// </summary>
public class GoComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.Go;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["go-module"];

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

        // Syft provides the h1 digest hash in metadata.H1Digest
        var hash = artifact.Metadata?.H1Digest;

        if (!string.IsNullOrWhiteSpace(hash))
        {
            return new GoComponent(name: artifact.Name, version: artifact.Version, hash: hash);
        }

        return new GoComponent(name: artifact.Name, version: artifact.Version);
    }
}
