#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="RubyGemsComponent"/> instances from Ruby gem artifacts.
/// </summary>
public class RubyGemsComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.RubyGems;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["gem"];

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

        // Syft may provide source as a string or as part of Dist
        var source =
            artifact.Metadata?.Source?.Dist?.Url
            ?? artifact.Metadata?.Source?.String
            ?? string.Empty;

        return new RubyGemsComponent(
            name: artifact.Name,
            version: artifact.Version,
            source: source
        );
    }
}
