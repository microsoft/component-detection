#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Factories;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;

/// <summary>
/// Factory interface for creating TypedComponent instances from Syft artifacts.
/// </summary>
public interface IArtifactComponentFactory
{
    /// <summary>
    /// Gets the artifact types (e.g., "npm", "apk", "deb") that this factory can handle.
    /// </summary>
    /// <remarks>
    /// For a complete list of Syft artifact types, see:
    /// https://github.com/anchore/syft/blob/main/syft/pkg/type.go.
    /// </remarks>
    public IEnumerable<string> SupportedArtifactTypes { get; }

    /// <summary>
    /// Creates a TypedComponent from a Syft artifact element.
    /// </summary>
    /// <param name="artifact">The artifact element from Syft output.</param>
    /// <param name="distro">The distribution information from Syft output.</param>
    /// <returns>A TypedComponent instance, or null if the artifact cannot be processed.</returns>
    public TypedComponent CreateComponent(ArtifactElement artifact, Distro distro);
}
