#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
/// Represents a service for interacting with the Docker daemon.
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Gets a value indicating whether the Docker daemon is running in Linux container mode.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if the Docker daemon is running and in Linux container mode, otherwise false.</returns>
    Task<bool> CanRunLinuxContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the Docker daemon can be pinged.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if the Docker daemon can be pinged, otherwise false.</returns>
    Task<bool> CanPingDockerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the container image exists locally.
    /// </summary>
    /// <param name="image">The image to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if the container image exists locally, otherwise false.</returns>
    Task<bool> ImageExistsLocallyAsync(string image, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls the container image from the Docker registry.
    /// </summary>
    /// <param name="image">The image to pull.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if the image was pulled successfully, otherwise false.</returns>
    Task<bool> TryPullImageAsync(string image, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the container image details.
    /// </summary>
    /// <param name="image">The image to inspect.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the container image details.</returns>
    Task<ContainerDetails> InspectImageAsync(string image, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and runs a container with the given image and command.
    /// </summary>
    /// <param name="image">The image to inspect.</param>
    /// <param name="command">The command to run in the container.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple of stdout and stderr from the container.</returns>
    Task<(string Stdout, string Stderr)> CreateAndRunContainerAsync(string image, IList<string> command, CancellationToken cancellationToken = default);
}
