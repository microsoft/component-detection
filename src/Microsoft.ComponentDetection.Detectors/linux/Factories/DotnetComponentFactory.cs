namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory for creating <see cref="NuGetComponent"/> instances from .NET package artifacts.
/// </summary>
public class DotnetComponentFactory : ArtifactComponentFactoryBase
{
    /// <inheritdoc/>
    public override IEnumerable<string> SupportedArtifactTypes => ["dotnet"];

    /// <inheritdoc/>
    public override TypedComponent? CreateComponent([NotNull] ArtifactElement artifact, [NotNull] Distro distro)
    {
        if (string.IsNullOrWhiteSpace(artifact.Name) || string.IsNullOrWhiteSpace(artifact.Version))
        {
            return null;
        }

        var author = GetAuthorFromArtifact(artifact);
        var authors = string.IsNullOrWhiteSpace(author) ? null : new[] { author };

        return new NuGetComponent(
            name: artifact.Name,
            version: artifact.Version,
            authors: authors);
    }
}
