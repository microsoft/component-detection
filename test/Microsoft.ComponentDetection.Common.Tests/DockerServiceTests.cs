#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerServiceTests
{
    private const string LinuxTestImage = "governancecontainerregistry.azurecr.io/testcontainers/hello-world:latest";
    private const string WindowsTestImage = "mcr.microsoft.com/windows/nanoserver:ltsc2025";

    private const string LinuxTestImageWithBaseDetails = "governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag";

    private readonly Mock<ILogger<DockerService>> loggerMock = new();
    private readonly DockerService dockerService;

    public DockerServiceTests() => this.dockerService = new DockerService(this.loggerMock.Object);

    /// <summary>
    /// Skip the test if docker is not running.
    /// </summary>
    private async Task SkipIfDockerNotRunningAsync()
    {
        var isDockerRunning = await this.dockerService.CanPingDockerAsync();
        if (!isDockerRunning)
        {
            Assert.Inconclusive("docker is not running");
        }
    }

    [TestMethod]
    public async Task DockerService_CanPingDockerAsync_DoesNotThrow()
    {
        // CanPingDockerAsync should return true or false, regardless of whether docker is running
        await this.dockerService.CanPingDockerAsync();
    }

    [TestMethod]
    public async Task DockerService_CanRunLinuxContainersAsync_DoesNotThrow()
    {
        await this.SkipIfDockerNotRunningAsync();

        // CanPingDockerAsync should return true or false if docker is running
        await this.dockerService.CanRunLinuxContainersAsync();
    }

    [TestMethod]
    public async Task DockerService_CanPullImageAsync()
    {
        await this.SkipIfDockerNotRunningAsync();

        var canRunLinuxContainers = await this.dockerService.CanRunLinuxContainersAsync();
        var testImage = canRunLinuxContainers ? LinuxTestImage : WindowsTestImage;

        var isImagePulled = await this.dockerService.TryPullImageAsync(testImage);
        isImagePulled.Should().BeTrue();
    }

    [TestMethod]
    public async Task DockerService_CanInspectImageAsync()
    {
        await this.SkipIfDockerNotRunningAsync();

        var canRunLinuxContainers = await this.dockerService.CanRunLinuxContainersAsync();
        var testImage = canRunLinuxContainers ? LinuxTestImage : WindowsTestImage;

        await this.dockerService.TryPullImageAsync(testImage);
        var details = await this.dockerService.InspectImageAsync(testImage);
        details.Should().NotBeNull();
        details.Tags.Should().Contain(testImage);
    }

    [TestMethod]
    public async Task DockerService_PopulatesBaseImageAndLayerDetailsAsync()
    {
        await this.SkipIfDockerNotRunningAsync();

        await this.dockerService.TryPullImageAsync(LinuxTestImageWithBaseDetails);
        var details = await this.dockerService.InspectImageAsync(LinuxTestImageWithBaseDetails);

        details.Should().NotBeNull();
        details.Tags.Should().Contain("governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag");
        var expectedCreatedAt = DateTime.Parse("2021-09-23T23:47:57.442225064Z").ToUniversalTime();

        details.Should().NotBeNull();
        details.Id.Should().BePositive();

        // The image ID depends on the Docker storage backend:
        // - Legacy graphdriver: returns the config digest
        // - containerd image store (default since Docker 29): returns the manifest digest
        var configDigest = "sha256:5edc12e9a797b59b9209354ff99d8550e7a1f90ca924c103fa3358e1a9ce15fe";
        var manifestDigest = "sha256:144c8d7e446fa9da415418ef7844ab87ad8fd93a0ca48919c29cf82150c81982";
        details.ImageId.Should().BeOneOf(configDigest, manifestDigest);
        details.CreatedAt.ToUniversalTime().Should().Be(expectedCreatedAt);
        details.BaseImageDigest.Should().Be("sha256:feb5d9fea6a5e9606aa995e879d862b825965ba48de054caab5ef356dc6b3412");
        details.BaseImageRef.Should().Be("docker.io/library/hello-world:latest");
        details.Layers.Should().ContainSingle();
    }

    [TestMethod]
    public async Task DockerService_CanCreateAndRunImageAsync()
    {
        await this.SkipIfDockerNotRunningAsync();

        var (stdout, stderr) = await this.dockerService.CreateAndRunContainerAsync(LinuxTestImage, []);
        stdout.Should().StartWith("\nHello from Docker!");
        stderr.Should().BeEmpty();
    }

    [TestMethod]
    public void DockerService_SanitizeEnvironmentVariables()
    {
        var responseInput = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                    "PIP_INDEX_URL=https://user:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@someregistry.localhost.com",
                ],
            },
        };

        var expected = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                    $"PIP_INDEX_URL=https://{StringUtilities.SensitivePlaceholder}@someregistry.localhost.com",
                ],
            },
        };

        this.dockerService.SanitizeEnvironmentVariables(responseInput);
        responseInput.Should().BeEquivalentTo(expected);

        responseInput = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                ],
            },
        };

        expected = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                ],
            },
        };

        this.dockerService.SanitizeEnvironmentVariables(responseInput);
        responseInput.Should().BeEquivalentTo(expected);

        responseInput = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                    "PIP_INDEX_URL=https://someregistry.localhost.com",
                ],
            },
        };

        expected = new ImageInspectResponse
        {
            Config = new Config
            {
                Env =
                [
                    "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    "MARATHON_APP_RESOURCE_CPU=1",
                    "REGION=local",
                    "PIP_INDEX_URL=https://someregistry.localhost.com",
                ],
            },
        };

        this.dockerService.SanitizeEnvironmentVariables(responseInput);
        responseInput.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void DockerService_SanitizeEnvironmentVariables_DoesNotThrow()
    {
        var responseInput = new ImageInspectResponse
        {
            Config = new Config
            {
                Env = null,
            },
        };

        var action = () => this.dockerService.SanitizeEnvironmentVariables(responseInput);
        action.Should().NotThrow();
        responseInput.Should().BeEquivalentTo(responseInput);

        responseInput = new ImageInspectResponse
        {
            Config = null,
        };

        action = () => this.dockerService.SanitizeEnvironmentVariables(responseInput);
        action.Should().NotThrow();
        responseInput.Should().BeEquivalentTo(responseInput);

        responseInput = null;

        action = () => this.dockerService.SanitizeEnvironmentVariables(responseInput);
        action.Should().NotThrow();
        responseInput.Should().BeNull();

        responseInput = new ImageInspectResponse
        {
            Config = new Config
            {
                Env = [],
            },
        };

        action = () => this.dockerService.SanitizeEnvironmentVariables(responseInput);
        action.Should().NotThrow();
        responseInput.Should().BeEquivalentTo(responseInput);
    }
}
