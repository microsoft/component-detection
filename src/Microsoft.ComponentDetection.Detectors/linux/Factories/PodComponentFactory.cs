#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="PodComponent"/> instances from CocoaPods artifacts.
/// </summary>
public class PodComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.Pod;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["pod"];

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

        // Syft does not currently provide spec repo information directly
        // but could be extracted from locations or other metadata if available
        return new PodComponent(name: artifact.Name, version: artifact.Version);
    }
}
