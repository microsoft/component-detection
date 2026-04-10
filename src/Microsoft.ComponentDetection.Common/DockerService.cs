#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.Extensions.Logging;

internal class DockerService : IDockerService
{
    // Base image annotations from ADO dockerTask
    private const string BaseImageRefAnnotation = "image.base.ref.name";
    private const string BaseImageDigestAnnotation = "image.base.digest";

    private static readonly DockerClient Client = new DockerClientConfiguration().CreateClient();
    private static int incrementingContainerId;

    private readonly ILogger logger;

    public DockerService(ILogger<DockerService> logger) => this.logger = logger;

    public async Task<bool> CanPingDockerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Client.System.PingAsync(cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to ping docker");
            return false;
        }
    }

    public async Task<bool> CanRunLinuxContainersAsync(CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceSystemInfoTelemetryRecord();
        if (!await this.CanPingDockerAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            var systemInfoResponse = await Client.System.GetSystemInfoAsync(cancellationToken);
            record.SystemInfo = JsonSerializer.Serialize(systemInfoResponse);
            return string.Equals(systemInfoResponse.OSType, "linux", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
        }

        return false;
    }

    public async Task<bool> ImageExistsLocallyAsync(string image, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceImageExistsLocallyTelemetryRecord
        {
            Image = image,
        };
        try
        {
            var imageInspectResponse = await this.InspectImageAndSanitizeVarsAsync(image, cancellationToken);
            record.ImageInspectResponse = JsonSerializer.Serialize(imageInspectResponse);
            return true;
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
            return false;
        }
    }

    private async Task<ImageInspectResponse> InspectImageAndSanitizeVarsAsync(string image, CancellationToken cancellationToken = default)
    {
        var imageInspectResponse = await Client.Images.InspectImageAsync(image, cancellationToken);
        this.SanitizeEnvironmentVariables(imageInspectResponse);
        return imageInspectResponse;
    }

    public async Task<bool> TryPullImageAsync(string image, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceTryPullImageTelemetryRecord
        {
            Image = image,
        };
        var parameters = new ImagesCreateParameters
        {
            FromImage = image,
        };
        try
        {
            var createImageProgress = new List<string>();
            var progress = new Progress<JSONMessage>(message =>
            {
                createImageProgress.Add(JsonSerializer.Serialize(message));
            });
            await Client.Images.CreateImageAsync(parameters, null, progress, cancellationToken);
            record.CreateImageProgress = JsonSerializer.Serialize(createImageProgress);
            return true;
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
            return false;
        }
    }

    internal void SanitizeEnvironmentVariables(ImageInspectResponse inspectResponse)
    {
        var envVariables = inspectResponse?.Config?.Env;
        if (envVariables == null || !envVariables.Any())
        {
            return;
        }

        var sanitizedVarList = new List<string>();
        foreach (var variable in inspectResponse.Config.Env)
        {
            sanitizedVarList.Add(variable.RemoveSensitiveInformation());
        }

        inspectResponse.Config.Env = sanitizedVarList;
    }

    public async Task<ContainerDetails> InspectImageAsync(string image, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceInspectImageTelemetryRecord
        {
            Image = image,
        };
        try
        {
            var imageInspectResponse = await this.InspectImageAndSanitizeVarsAsync(image, cancellationToken);
            record.ImageInspectResponse = JsonSerializer.Serialize(imageInspectResponse);

            var baseImageRef = string.Empty;
            var baseImageDigest = string.Empty;

            imageInspectResponse.Config.Labels?.TryGetValue(BaseImageRefAnnotation, out baseImageRef);
            imageInspectResponse.Config.Labels?.TryGetValue(BaseImageDigestAnnotation, out baseImageDigest);

            record.BaseImageRef = baseImageRef;
            record.BaseImageDigest = baseImageDigest;

            var layers = imageInspectResponse.RootFS?.Layers
                .Select((diffId, index) =>
                    new DockerLayer
                    {
                        DiffId = diffId,
                        LayerIndex = index,
                    });

            return new ContainerDetails
            {
                Id = GetContainerId(),
                ImageId = imageInspectResponse.ID,
                Digests = imageInspectResponse.RepoDigests,
                Tags = imageInspectResponse.RepoTags,
                CreatedAt = imageInspectResponse.Created,
                BaseImageDigest = baseImageDigest,
                BaseImageRef = baseImageRef,
                Layers = layers ?? [],
            };
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
            return null;
        }
    }

    public async Task<(string Stdout, string Stderr)> CreateAndRunContainerAsync(string image, IList<string> command, CancellationToken cancellationToken = default)
    {
        return await this.CreateAndRunContainerAsync(image, command, additionalBinds: null, cancellationToken);
    }

    public async Task<(string Stdout, string Stderr)> CreateAndRunContainerAsync(string image, IList<string> command, IList<string> additionalBinds, CancellationToken cancellationToken = default)
    {
        var commandJson = JsonSerializer.Serialize(command);

        // Summary record captures overall operation including stdout/stderr
        using var record = new DockerServiceTelemetryRecord
        {
            Image = image,
            Command = commandJson,
        };

        await this.TryPullImageAsync(image, cancellationToken);
        var container = await CreateContainerAsync(image, command, additionalBinds, cancellationToken);
        record.Container = JsonSerializer.Serialize(container);

        try
        {
            var stream = await AttachContainerAsync(container.ID, cancellationToken);
            await StartContainerAsync(container.ID, cancellationToken);

            var (stdout, stderr) = await ReadContainerOutputAsync(stream, container.ID, image, cancellationToken);

            record.Stdout = stdout;
            record.Stderr = stderr;

            return (stdout, stderr);
        }
        finally
        {
            // Best-effort container cleanup with a bounded timeout.
            // RemoveContainerAsync already handles not-found, but we must guard against
            // the Docker daemon hanging on container removal (e.g. when the container
            // process is stuck), which would block the detector indefinitely.
            using var removeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await RemoveContainerAsync(container.ID, removeCts.Token);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Failed to remove container {ContainerId}; abandoning cleanup",
                    container.ID);
            }
        }
    }

    /// <summary>
    /// Reads container output with proper cancellation support.
    /// ReadOutputToEndAsync doesn't properly honor cancellation when blocked on socket read,
    /// so we race it against a cancellation-aware delay and dispose the stream if cancelled.
    /// </summary>
    private static async Task<(string Stdout, string Stderr)> ReadContainerOutputAsync(
        MultiplexedStream stream,
        string containerId,
        string image,
        CancellationToken cancellationToken)
    {
        using var record = new DockerServiceStepTelemetryRecord
        {
            Step = "ReadOutput",
            ContainerId = containerId,
            Image = image,
        };

        try
        {
            var readTask = stream.ReadOutputToEndAsync(CancellationToken.None);
            var delayTask = Task.Delay(Timeout.Infinite, cancellationToken);

            var completedTask = await Task.WhenAny(readTask, delayTask);

            if (completedTask == delayTask)
            {
                record.WasCancelled = true;

                // Dispose the stream to unblock any pending read operation
                stream.Dispose();

                // Observe the readTask to prevent unobserved task exceptions.
                // Running any continuation automatically marks the exception as observed.
                _ = readTask.ContinueWith(
                    static _ => { },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);

                // Caller is responsible for container cleanup via finally block
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await readTask;
        }
        catch (Exception ex)
        {
            record.ExceptionMessage = ex.Message;
            throw;
        }
    }

    private static async Task<CreateContainerResponse> CreateContainerAsync(
        string image,
        IList<string> command,
        IList<string> additionalBinds,
        CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceStepTelemetryRecord
        {
            Step = "CreateContainer",
            Image = image,
            Command = JsonSerializer.Serialize(command),
        };

        try
        {
            var binds = new List<string>
            {
                $"{Path.GetTempPath()}:/tmp",
                "/var/run/docker.sock:/var/run/docker.sock",
            };

            if (additionalBinds != null)
            {
                binds.AddRange(additionalBinds);
            }

            var parameters = new CreateContainerParameters
            {
                Image = image,
                Cmd = command,
                NetworkDisabled = true,
                HostConfig = new HostConfig
                {
                    CapDrop =
                    [
                        "all",
                    ],
                    SecurityOpt =
                    [
                        "no-new-privileges",
                    ],
                    Binds = binds,
                },
            };

            var response = await Client.Containers.CreateContainerAsync(parameters, cancellationToken);
            record.ContainerId = response.ID;
            return response;
        }
        catch (Exception ex)
        {
            record.ExceptionMessage = ex.Message;
            throw;
        }
    }

    private static async Task<MultiplexedStream> AttachContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceStepTelemetryRecord
        {
            Step = "AttachContainer",
            ContainerId = containerId,
        };

        try
        {
            var parameters = new ContainerAttachParameters
            {
                Stdout = true,
                Stderr = true,
                Stream = true,
            };
            return await Client.Containers.AttachContainerAsync(containerId, false, parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            record.ExceptionMessage = ex.Message;
            throw;
        }
    }

    private static async Task StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceStepTelemetryRecord
        {
            Step = "StartContainer",
            ContainerId = containerId,
        };

        try
        {
            var parameters = new ContainerStartParameters();
            await Client.Containers.StartContainerAsync(containerId, parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            record.ExceptionMessage = ex.Message;
            throw;
        }
    }

    private static async Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceStepTelemetryRecord
        {
            Step = "RemoveContainer",
            ContainerId = containerId,
        };

        try
        {
            var parameters = new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = true,
            };
            await Client.Containers.RemoveContainerAsync(containerId, parameters, cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // Container already removed - this is expected during cleanup
        }
        catch (Exception ex)
        {
            record.ExceptionMessage = ex.Message;
            throw;
        }
    }

    private static int GetContainerId()
    {
        return Interlocked.Increment(ref incrementingContainerId);
    }

    /// <inheritdoc/>
    public ContainerDetails GetEmptyContainerDetails()
    {
        return new ContainerDetails { Id = GetContainerId() };
    }
}
