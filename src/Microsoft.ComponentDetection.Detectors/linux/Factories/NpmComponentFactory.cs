namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="NpmComponent"/> instances from npm package artifacts.
/// </summary>
public class NpmComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["npm"];

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

        var author = GetNpmAuthorFromArtifact(artifact);
        var hash = GetHashFromArtifact(artifact);

        return new NpmComponent(
            name: artifact.Name,
            version: artifact.Version,
            hash: hash,
            author: author);
    }

    private static NpmAuthor GetNpmAuthorFromArtifact(ArtifactElement artifact)
    {
        var authorString = artifact.Metadata?.Author;
        if (!string.IsNullOrWhiteSpace(authorString))
        {
            return new NpmAuthor(authorString);
        }

        return null;
    }

    private static string GetHashFromArtifact(ArtifactElement artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.Metadata?.Integrity))
        {
            return artifact.Metadata.Integrity;
        }

        return null;
    }
}
