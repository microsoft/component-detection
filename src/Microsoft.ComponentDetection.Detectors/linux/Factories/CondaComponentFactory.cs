#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="CondaComponent"/> instances from Conda package artifacts.
/// </summary>
public class CondaComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.Conda;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["conda"];

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

        // Syft provides conda metadata including build, channel, subdir, url, and md5
        var metadata = artifact.Metadata;

        return new CondaComponent(
            name: artifact.Name,
            version: artifact.Version,
            build: metadata?.Build,
            channel: metadata?.Channel,
            subdir: metadata?.Subdir,
            @namespace: null, // Syft doesn't provide namespace
            url: metadata?.Url?.String,
            md5: metadata?.Md5
        );
    }
}
