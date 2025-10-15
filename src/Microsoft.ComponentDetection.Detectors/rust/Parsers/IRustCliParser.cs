namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;

/// <summary>
/// Provides methods to parse Rust <c>Cargo.toml</c> / workspace dependency information into the component
/// recording system. Implementations may choose to invoke the Rust CLI (e.g. <c>cargo metadata</c>) or
/// operate on already-supplied serialized metadata.
/// </summary>
/// <remarks>
/// There are three entry points:
/// 1. <see cref="ParseAsync"/> triggers a fresh acquisition (typically by invoking the Cargo CLI).
/// 2. <see cref="ParseFromMetadataAsync(IComponentStream, ISingleFileComponentRecorder, CargoMetadata, IComponentRecorder, IReadOnlyDictionary{string, HashSet{string}}, CancellationToken)"/> consumes a pre-fetched <see cref="CargoMetadata"/> blob.
/// 3. <see cref="ParseFromMetadataAsync(IComponentStream, ISingleFileComponentRecorder, CargoMetadata, IComponentRecorder, IReadOnlyDictionary{string, HashSet{string}}, CancellationToken)"/> adds support
///    for multi-file / workspace ownership resolution by leveraging a parent recorder and an ownership map.
/// </remarks>
public interface IRustCliParser
{
    /// <summary>
    /// Parses Rust dependency information for the supplied component stream (generally a <c>Cargo.toml</c>)
    /// by invoking the Cargo CLI (e.g. running <c>cargo metadata</c>) and recording discovered components
    /// and dependency edges into the provided <paramref name="recorder"/>.
    /// </summary>
    /// <param name="componentStream">The stream representing the manifest file being parsed.</param>
    /// <param name="recorder">The per-file component recorder used to register detected components and graph edges.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the parse operation.</param>
    /// <returns>
    /// A <see cref="ParseResult"/> indicating success or failure (with failure reason and any relevant local
    /// package directories that were resolved).
    /// </returns>
    public Task<ParseResult> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken);

    /// <summary>
    /// Parses Rust dependency information using a pre-obtained <see cref="CargoMetadata"/> object, with support
    /// for attributing discovered packages to owning manifests in a multi-project / workspace scenario.
    /// </summary>
    /// <param name="componentStream">The manifest stream being processed (used primarily for location context).</param>
    /// <param name="fallbackRecorder">
    /// A single-file recorder used if ownership cannot be resolved to a more specific recorder via the
    /// <paramref name="ownershipMap"/> or <paramref name="parentComponentRecorder"/>.
    /// </param>
    /// <param name="cachedMetadata">The pre-fetched Cargo metadata describing packages and their relationships.</param>
    /// <param name="parentComponentRecorder">
    /// The parent recorder that can produce (or correlate) other single-file recorders used to correctly
    /// attribute dependencies to their originating manifest locations.
    /// </param>
    /// <param name="ownershipMap">
    /// A mapping of package ID (or equivalent key) to a set of manifest file paths indicating ownership.
    /// Used to decide which recorder should own which package entries.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ParseResult"/> containing success state and any local package directories discovered.</returns>
    public Task<ParseResult> ParseFromMetadataAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder fallbackRecorder,
        CargoMetadata cachedMetadata,
        IComponentRecorder parentComponentRecorder,
        IReadOnlyDictionary<string, HashSet<string>> ownershipMap,
        CancellationToken cancellationToken);

    /// <summary>
    /// Result of parsing a Cargo.toml file.
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether parsing was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if parsing failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the reason for failure if parsing failed.
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Gets or sets the local package directories that should be marked as visited.
        /// This allows upstream client to skip TOMLs that were already accounted for in this run.
        /// </summary>
        public HashSet<string> LocalPackageDirectories { get; set; } = [];
    }
}
