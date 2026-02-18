#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="MavenComponent"/> instances from Java archive artifacts.
/// </summary>
public class MavenComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override ComponentType SupportedComponentType => ComponentType.Maven;

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["java-archive"];

    /// <inheritdoc/>
    public override TypedComponent CreateComponent(ArtifactElement artifact, Distro distro)
    {
        if (artifact == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(artifact.Version))
        {
            return null;
        }

        // Syft provides Maven coordinates in pomProperties or pomProject
        var pomProperties = artifact.Metadata?.PomProperties;
        var pomProject = artifact.Metadata?.PomProject;

        // Try pomProperties first (more reliable for resolved dependencies)
        var groupId = pomProperties?.GroupId ?? pomProject?.GroupId;
        var artifactId = pomProperties?.ArtifactId ?? pomProject?.ArtifactId;

        // Fall back to artifact name if no pom metadata available
        // Syft uses the format "groupId:artifactId" or just the jar name
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(artifactId))
        {
            if (!TryParseFromName(artifact.Name, out groupId, out artifactId))
            {
                // Cannot determine Maven coordinates
                return null;
            }
        }

        return new MavenComponent(
            groupId: groupId,
            artifactId: artifactId,
            version: artifact.Version
        );
    }

    /// <summary>
    /// Attempts to parse groupId and artifactId from a name in the format "groupId:artifactId"
    /// or similar Maven coordinate notation.
    /// </summary>
    private static bool TryParseFromName(string name, out string groupId, out string artifactId)
    {
        groupId = null;
        artifactId = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Handle "groupId:artifactId" format
        var colonIndex = name.IndexOf(':');
        if (colonIndex > 0 && colonIndex < name.Length - 1)
        {
            groupId = name[..colonIndex];
            artifactId = name[(colonIndex + 1)..];
            return !string.IsNullOrWhiteSpace(groupId) && !string.IsNullOrWhiteSpace(artifactId);
        }

        return false;
    }
}
