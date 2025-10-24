#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerServiceTests
{
    private const string TestImage = "governancecontainerregistry.azurecr.io/testcontainers/hello-world:latest";

    private const string TestImageWithBaseDetails = "governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag";

    private readonly Mock<ILogger<DockerService>> loggerMock = new();
    private readonly DockerService dockerService;

    public DockerServiceTests() => this.dockerService = new DockerService(this.loggerMock.Object);

    [TestMethod]
    public async Task DockerService_CanPingDockerAsync()
    {
        var canPingDocker = await this.dockerService.CanPingDockerAsync();
        canPingDocker.Should().BeTrue();
    }

    [SkipTestOnWindows]
    public async Task DockerService_CanRunLinuxContainersAsync()
    {
        var isLinuxContainerModeEnabled = await this.dockerService.CanRunLinuxContainersAsync();
        isLinuxContainerModeEnabled.Should().BeTrue();
    }

    [SkipTestOnWindows]
    public async Task DockerService_CanPullImageAsync()
    {
        Func<Task> action = async () => await this.dockerService.TryPullImageAsync(TestImage);
        await action.Should().NotThrowAsync();
    }

    [SkipTestOnWindows]
    public async Task DockerService_CanInspectImageAsync()
    {
        await this.dockerService.TryPullImageAsync(TestImage);
        var details = await this.dockerService.InspectImageAsync(TestImage);
        details.Should().NotBeNull();
        details.Tags.Should().Contain("governancecontainerregistry.azurecr.io/testcontainers/hello-world:latest");
    }

    [SkipTestOnWindows]
    public async Task DockerService_PopulatesBaseImageAndLayerDetailsAsync()
    {
        await this.dockerService.TryPullImageAsync(TestImageWithBaseDetails);
        var details = await this.dockerService.InspectImageAsync(TestImageWithBaseDetails);

        details.Should().NotBeNull();
        details.Tags.Should().Contain("governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag");
        var expectedImageId = "sha256:5edc12e9a797b59b9209354ff99d8550e7a1f90ca924c103fa3358e1a9ce15fe";
        var expectedCreatedAt = DateTime.Parse("2021-09-23T23:47:57.442225064Z").ToUniversalTime();

        details.Should().NotBeNull();
        details.Id.Should().BePositive();
        details.ImageId.Should().BeEquivalentTo(expectedImageId);
        details.CreatedAt.ToUniversalTime().Should().Be(expectedCreatedAt);
        details.BaseImageDigest.Should().Be("sha256:feb5d9fea6a5e9606aa995e879d862b825965ba48de054caab5ef356dc6b3412");
        details.BaseImageRef.Should().Be("docker.io/library/hello-world:latest");
        details.Layers.Should().ContainSingle();
    }

    [SkipTestOnWindows]
    public async Task DockerService_CanCreateAndRunImageAsync()
    {
        var (stdout, stderr) = await this.dockerService.CreateAndRunContainerAsync(TestImage, []);
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
