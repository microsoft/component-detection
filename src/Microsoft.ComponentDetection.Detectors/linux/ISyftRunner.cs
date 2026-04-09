namespace Microsoft.ComponentDetection.Detectors.Linux;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for executing Syft scans against container images.
/// Implementations may invoke Syft via Docker container or as a local binary.
/// </summary>
public interface ISyftRunner
{
    /// <summary>
    /// Checks whether this runner is able to execute Syft scans in the current environment.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the runner is ready to execute scans, false otherwise.</returns>
    Task<bool> CanRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Syft against a container image and returns the raw output.
    /// </summary>
    /// <param name="imageReference">The image reference to scan. Each runner implementation handles this differently based on the image kind.</param>
    /// <param name="arguments">The command-line arguments to pass to Syft (e.g., --quiet, --output json, --scope).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the standard output and standard error from the Syft execution.</returns>
    Task<(string Stdout, string Stderr)> RunSyftAsync(
        ImageReference imageReference,
        IList<string> arguments,
        CancellationToken cancellationToken = default);
}
