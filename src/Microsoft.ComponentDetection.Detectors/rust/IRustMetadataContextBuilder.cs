#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;

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
    /// and returns additional ownership metadata. Also returns a cache of raw Cargo metadata
    /// per manifest so downstream detection code can avoid invoking <c>cargo metadata</c> again.
    /// </summary>
    /// <param name="orderedTomlPaths">
    /// An ordered enumeration of paths to <c>Cargo.toml</c> manifest files. The order should
    /// reflect dependency resolution (e.g. workspace root manifests first, followed by members).
    /// </param>
    /// <param name="cancellationToken">A token used to observe cancellation requests.</param>
    /// <returns>An <see cref="OwnershipResult"/> with ownership and per-manifest metadata cache.</returns>
    public Task<OwnershipResult> BuildPackageOwnershipMapAsync(
        IEnumerable<string> orderedTomlPaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// Represents the result of building Rust package ownership metadata.
    /// </summary>
    public class OwnershipResult
    {
        /// <summary>
        /// Mapping from a package (crate) id to all <c>Cargo.toml</c> manifest
        /// paths that declare or reference that package.
        /// </summary>
        public Dictionary<string, HashSet<string>> PackageToTomls { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set of <c>Cargo.toml</c> manifest paths that declare local packages.
        /// </summary>
        public HashSet<string> LocalPackageManifests { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raw <c>cargo metadata</c> JSON (already parsed) per manifest path (normalized).
        /// This enables downstream detectors to reuse metadata without issuing
        /// another CLI call.
        /// </summary>
        public Dictionary<string, CargoMetadata> ManifestToMetadata { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        // Manifests for which cargo metadata failed.
        public HashSet<string> FailedManifests { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
