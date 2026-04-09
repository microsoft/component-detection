namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs Syft by executing a Docker container with the Syft image.
/// </summary>
internal class DockerSyftRunner : IDockerSyftRunner
{
    internal const string ScannerImage =
        "governancecontainerregistry.azurecr.io/syft:v1.37.0@sha256:48d679480c6d272c1801cf30460556959c01d4826795be31d4fd8b53750b7d91";

    private const string LocalImageMountPoint = "/image";

    private static readonly SemaphoreSlim ContainerSemaphore = new(2);

    private static readonly int SemaphoreTimeout = Convert.ToInt32(
        TimeSpan.FromHours(1).TotalMilliseconds);

    private readonly IDockerService dockerService;
    private readonly ILogger<DockerSyftRunner> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerSyftRunner"/> class.
    /// </summary>
    /// <param name="dockerService">The docker service.</param>
    /// <param name="logger">The logger.</param>
    public DockerSyftRunner(IDockerService dockerService, ILogger<DockerSyftRunner> logger)
    {
        this.dockerService = dockerService;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CanRunAsync(CancellationToken cancellationToken = default)
    {
        if (await this.dockerService.CanRunLinuxContainersAsync(cancellationToken))
        {
            return true;
        }

        using var record = new LinuxContainerDetectorUnsupportedOs
        {
            Os = RuntimeInformation.OSDescription,
        };
        this.logger.LogInformation("Linux containers are not available on this host.");
        return false;
    }

    /// <inheritdoc/>
    public async Task<(string Stdout, string Stderr)> RunSyftAsync(
        ImageReference imageReference,
        IList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var (syftSource, additionalBinds) = GetSyftSourceAndBinds(imageReference);
        var acquired = false;

        try
        {
            acquired = await ContainerSemaphore.WaitAsync(SemaphoreTimeout, cancellationToken);
            if (!acquired)
            {
                this.logger.LogWarning(
                    "Failed to enter the container semaphore for image {ImageReference}",
                    imageReference.Reference);
                return (string.Empty, string.Empty);
            }

            var command = new List<string> { syftSource }
                .Concat(arguments)
                .ToList();

            return await this.dockerService.CreateAndRunContainerAsync(
                ScannerImage,
                command,
                additionalBinds,
                cancellationToken);
        }
        finally
        {
            if (acquired)
            {
                ContainerSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Constructs the Syft source argument and any required Docker bind mounts from an image reference.
    /// For Docker images, no additional binds are needed. For local images (OCI/archives),
    /// the host path is mounted into the container and the source uses the container-relative path.
    /// </summary>
    private static (string SyftSource, IList<string> AdditionalBinds) GetSyftSourceAndBinds(ImageReference imageReference)
    {
        switch (imageReference.Kind)
        {
            case ImageReferenceKind.DockerImage:
                return (imageReference.Reference, []);

            case ImageReferenceKind.OciLayout:
                return (
                    $"oci-dir:{LocalImageMountPoint}",
                    [$"{imageReference.Reference}:{LocalImageMountPoint}:ro"]);

            case ImageReferenceKind.OciArchive:
            {
                var dir = Path.GetDirectoryName(imageReference.Reference)
                    ?? throw new InvalidOperationException($"Could not determine parent directory for OCI archive path '{imageReference.Reference}'.");
                var fileName = Path.GetFileName(imageReference.Reference);
                return (
                    $"oci-archive:{LocalImageMountPoint}/{fileName}",
                    [$"{dir}:{LocalImageMountPoint}:ro"]);
            }

            case ImageReferenceKind.DockerArchive:
            {
                var dir = Path.GetDirectoryName(imageReference.Reference)
                    ?? throw new InvalidOperationException($"Could not determine parent directory for Docker archive path '{imageReference.Reference}'.");
                var fileName = Path.GetFileName(imageReference.Reference);
                return (
                    $"docker-archive:{LocalImageMountPoint}/{fileName}",
                    [$"{dir}:{LocalImageMountPoint}:ro"]);
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(imageReference),
                    $"Unsupported image reference kind '{imageReference.Kind}'.");
        }
    }
}
