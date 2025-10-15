namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using static Microsoft.ComponentDetection.Detectors.Rust.RustCliParser;

public interface IRustCliParser
{
    /// <summary>
    /// Parses a Cargo.toml file by invoking 'cargo metadata'.
    /// </summary>
    /// <param name="componentStream">The component stream containing the Cargo.toml file.</param>
    /// <param name="recorder">The component recorder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parse result containing success status and local package directories.</returns>
    public Task<ParseResult> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken);

    /// <summary>
    /// Parses a Cargo.toml file using a previously obtained CargoMetadata (cached output).
    /// Avoids re-running the cargo command.
    /// </summary>
    /// <returns>Result of parsing cargo metadata.</returns>
    public Task<ParseResult> ParseFromMetadataAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder recorder,
        CargoMetadata cachedMetadata,
        CancellationToken cancellationToken);
}
