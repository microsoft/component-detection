namespace Microsoft.ComponentDetection.Common;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Newtonsoft.Json;

[Export(typeof(IDockerService))]
public class DockerService : IDockerService
{
    // Base image annotations from ADO dockerTask
    private const string BaseImageRefAnnotation = "image.base.ref.name";
    private const string BaseImageDigestAnnotation = "image.base.digest";

    private static readonly DockerClient Client = new DockerClientConfiguration().CreateClient();
    private static int incrementingContainerId;

    [Import]
    public ILogger Logger { get; set; }

    public async Task<bool> CanPingDockerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Client.System.PingAsync(cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            this.Logger.LogException(e, false);
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
            record.SystemInfo = JsonConvert.SerializeObject(systemInfoResponse);
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
            var imageInspectResponse = await Client.Images.InspectImageAsync(image, cancellationToken);
            record.ImageInspectResponse = JsonConvert.SerializeObject(imageInspectResponse);
            return true;
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
            return false;
        }
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
                createImageProgress.Add(JsonConvert.SerializeObject(message));
            });
            await Client.Images.CreateImageAsync(parameters, null, progress, cancellationToken);
            record.CreateImageProgress = JsonConvert.SerializeObject(createImageProgress);
            return true;
        }
        catch (Exception e)
        {
            record.ExceptionMessage = e.Message;
            return false;
        }
    }

    public async Task<ContainerDetails> InspectImageAsync(string image, CancellationToken cancellationToken = default)
    {
        using var record = new DockerServiceInspectImageTelemetryRecord
        {
            Image = image,
        };
        try
        {
            var imageInspectResponse = await Client.Images.InspectImageAsync(image, cancellationToken);
            record.ImageInspectResponse = JsonConvert.SerializeObject(imageInspectResponse);

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
                Layers = layers ?? Enumerable.Empty<DockerLayer>(),
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
        using var record = new DockerServiceTelemetryRecord
        {
            Image = image,
            Command = JsonConvert.SerializeObject(command),
        };
        await this.TryPullImageAsync(image, cancellationToken);
        var container = await CreateContainerAsync(image, command, cancellationToken);
        record.Container = JsonConvert.SerializeObject(container);
        var stream = await AttachContainerAsync(container.ID, cancellationToken);
        await StartContainerAsync(container.ID, cancellationToken);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        record.Stdout = stdout;
        record.Stderr = stderr;
        await RemoveContainerAsync(container.ID, cancellationToken);
        return (stdout, stderr);
    }

    private static async Task<CreateContainerResponse> CreateContainerAsync(
        string image,
        IList<string> command,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateContainerParameters
        {
            Image = image,
            Cmd = command,
            NetworkDisabled = true,
            HostConfig = new HostConfig
            {
                CapDrop = new List<string>
                {
                    "all",
                },
                SecurityOpt = new List<string>
                {
                    "no-new-privileges",
                },
                Binds = new List<string>
                {
                    $"{Path.GetTempPath()}:/tmp",
                    "/var/run/docker.sock:/var/run/docker.sock",
                },
            },
        };
        return await Client.Containers.CreateContainerAsync(parameters, cancellationToken);
    }

    private static async Task<MultiplexedStream> AttachContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var parameters = new ContainerAttachParameters
        {
            Stdout = true,
            Stderr = true,
            Stream = true,
        };
        return await Client.Containers.AttachContainerAsync(containerId, false, parameters, cancellationToken);
    }

    private static async Task StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var parameters = new ContainerStartParameters();
        await Client.Containers.StartContainerAsync(containerId, parameters, cancellationToken);
    }

    private static async Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var parameters = new ContainerRemoveParameters
        {
            Force = true,
            RemoveVolumes = true,
        };
        await Client.Containers.RemoveContainerAsync(containerId, parameters, cancellationToken);
    }

    private static int GetContainerId() => Interlocked.Increment(ref incrementingContainerId);
}
