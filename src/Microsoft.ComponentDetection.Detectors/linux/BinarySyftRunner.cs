namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs Syft by invoking a local Syft binary.
/// </summary>
internal class BinarySyftRunner : ISyftRunner
{
    private static readonly SemaphoreSlim BinarySemaphore = new(2);

    private static readonly int SemaphoreTimeout = Convert.ToInt32(
        TimeSpan.FromHours(1).TotalMilliseconds);

    private readonly string syftBinaryPath;
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly ILogger<BinarySyftRunner> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinarySyftRunner"/> class.
    /// </summary>
    /// <param name="syftBinaryPath">The path to the Syft binary.</param>
    /// <param name="commandLineInvocationService">The command line invocation service.</param>
    /// <param name="logger">The logger.</param>
    public BinarySyftRunner(
        string syftBinaryPath,
        ICommandLineInvocationService commandLineInvocationService,
        ILogger<BinarySyftRunner> logger)
    {
        this.syftBinaryPath = syftBinaryPath;
        this.commandLineInvocationService = commandLineInvocationService;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CanRunAsync(CancellationToken cancellationToken = default)
    {
        var result = await this.commandLineInvocationService.ExecuteCommandAsync(
            this.syftBinaryPath,
            null,
            null,
            cancellationToken,
            "--version");

        if (result.ExitCode != 0)
        {
            this.logger.LogInformation(
                "Syft binary at {SyftBinaryPath} failed version check with exit code {ExitCode}. Stderr: {StdErr}",
                this.syftBinaryPath,
                result.ExitCode,
                result.StdErr);
            return false;
        }

        this.logger.LogInformation(
            "Using Syft binary at {SyftBinaryPath}: {SyftVersion}",
            this.syftBinaryPath,
            result.StdOut?.Trim());
        return true;
    }

    /// <inheritdoc/>
    public async Task<(string Stdout, string Stderr)> RunSyftAsync(
        ImageReference imageReference,
        IList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var syftSource = GetSyftSource(imageReference);
        var acquired = false;

        try
        {
            acquired = await BinarySemaphore.WaitAsync(SemaphoreTimeout, cancellationToken);
            if (!acquired)
            {
                this.logger.LogWarning(
                    "Failed to enter the binary semaphore for image {ImageReference}",
                    imageReference.Reference);
                return (string.Empty, string.Empty);
            }

            var parameters = new[] { syftSource }
                .Concat(arguments)
                .ToArray();

            var result = await this.commandLineInvocationService.ExecuteCommandAsync(
                this.syftBinaryPath,
                null,
                null,
                cancellationToken,
                parameters);

            if (result.ExitCode != 0)
            {
                this.logger.LogError(
                    "Syft binary exited with code {ExitCode}. Stderr: {StdErr}",
                    result.ExitCode,
                    result.StdErr);
            }

            return (result.StdOut, result.StdErr);
        }
        finally
        {
            if (acquired)
            {
                BinarySemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Constructs the Syft source argument from an image reference.
    /// For local images, the host path is used directly with the appropriate scheme prefix.
    /// </summary>
    private static string GetSyftSource(ImageReference imageReference) =>
        imageReference.Kind switch
        {
            ImageReferenceKind.DockerImage => imageReference.Reference,
            ImageReferenceKind.OciLayout => $"oci-dir:{imageReference.Reference}",
            ImageReferenceKind.OciArchive => $"oci-archive:{imageReference.Reference}",
            ImageReferenceKind.DockerArchive => $"docker-archive:{imageReference.Reference}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(imageReference),
                $"Unsupported image reference kind '{imageReference.Kind}'."),
        };
}
