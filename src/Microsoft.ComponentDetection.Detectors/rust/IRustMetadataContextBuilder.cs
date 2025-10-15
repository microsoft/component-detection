namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides functionality to construct contextual metadata for Rust packages,
/// specifically mapping package (crate) names to the <c>Cargo.toml</c> manifests
/// that declare or reference them.
/// </summary>
public interface IRustMetadataContextBuilder
{
    /// <summary>
    /// Builds a mapping of package (crate) names to the set of <c>Cargo.toml</c> files
    /// (supplied in dependency resolution order) that either declare or reference them,
    /// and returns additional ownership metadata.
    /// </summary>
    /// <param name="orderedTomlPaths">
    /// An ordered enumeration of paths to <c>Cargo.toml</c> manifest files. The order should
    /// reflect dependency resolution (e.g. workspace root manifests first, followed by members),
    /// enabling deterministic ownership attribution.
    /// </param>
    /// <param name="cancellationToken">A token used to observe cancellation requests.</param>
    /// <returns>
    /// A task that, when completed successfully, yields an <see cref="OwnershipResult"/>
    /// containing the package-to-manifest ownership mapping and the set of manifests that
    /// represent locally declared (non-external) packages.
    /// </returns>
    /// <remarks>
    /// Implementations may perform file IO and parsing of TOML manifests; callers should
    /// provide a cancellation token for responsiveness. Implementations are expected to be
    /// case-insensitive with respect to crate and file path comparisons.
    /// </remarks>
    public Task<OwnershipResult> BuildPackageOwnershipMapAsync(
        IEnumerable<string> orderedTomlPaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// Represents the result of building Rust package ownership metadata.
    /// </summary>
    public class OwnershipResult
    {
        /// <summary>
        /// Gets or sets a mapping from a package (crate) name to all <c>Cargo.toml</c> manifest
        /// paths that declare or reference that package. Keys are compared using
        /// <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        public Dictionary<string, HashSet<string>> PackageToTomls { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the set of <c>Cargo.toml</c> manifest paths that declare local (non-external)
        /// packages within the current workspace or solution. Entries are compared using
        /// <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        public HashSet<string> LocalPackageManifests { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);
    }
}
