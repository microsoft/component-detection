using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class DockerServiceTests
    {
        private DockerService dockerService;

        private const string TestImage = "governancecontainerregistry.azurecr.io/testcontainers/hello-world:latest";
        
        private const string TestImageWithBaseDetails = "governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag";
        
        [TestInitialize]
        public void TestInitialize()
        {
            dockerService = new DockerService();
        }
        
        [TestMethod]
        public async Task DockerService_CanPingDocker()
        {
            var canPingDocker = await dockerService.CanPingDockerAsync();
            Assert.IsTrue(canPingDocker);
        }

        [SkipTestOnWindows]
        public async Task DockerService_CanRunLinuxContainersAsync()
        {
            var isLinuxContainerModeEnabled = await dockerService.CanRunLinuxContainersAsync();
            Assert.IsTrue(isLinuxContainerModeEnabled);
        }
        
        [SkipTestOnWindows]
        public async Task DockerService_CanPullImage()
        {
            Func<Task> action = async () => await dockerService.TryPullImageAsync(TestImage);
            await action.Should().NotThrowAsync();
        }
        
        [SkipTestOnWindows]
        public async Task DockerService_CanInspectImage()
        {
            await dockerService.TryPullImageAsync(TestImage);
            var details = await dockerService.InspectImageAsync(TestImage);
            details.Should().NotBeNull();
            details.Tags.Should().Contain("governancecontainerregistry.azurecr.io/testcontainers/hello-world:latest");
        }

        [SkipTestOnWindows]
        public async Task DockerService_PopulatesBaseImageAndLayerDetails()
        {
            await dockerService.TryPullImageAsync(TestImageWithBaseDetails);
            var details = await dockerService.InspectImageAsync(TestImageWithBaseDetails);

            details.Should().NotBeNull();
            details.Tags.Should().Contain("governancecontainerregistry.azurecr.io/testcontainers/dockertags_test:testtag");
            var expectedImageId = "sha256:8a311790d0b3414e97ed7b31c6ddf1711f980f4fca83b6ecb6becfa8c1867bfe";
            var expectedCreatedAt = DateTime.Parse("2021-07-28 19:25:20.3307716");

            details.Should().NotBeNull();
            details.Id.Should().BeGreaterThan(0);
            details.ImageId.Should().BeEquivalentTo(expectedImageId);
            details.CreatedAt.ToUniversalTime().Should().Be(expectedCreatedAt);
            details.BaseImageDigest.Should().Be("sha256:5c8908bc326c0b7c4f0f8059bbde31a92826446a88e6d7c7f6024b4d33fec545");
            details.BaseImageRef.Should().Be("ubuntu:precise-20151020");
            details.Layers.Should().HaveCount(4);
        }
        
        [SkipTestOnWindows]
        public async Task DockerService_CanCreateAndRunImage()
        {
            var (stdout, stderr) = await dockerService.CreateAndRunContainerAsync(TestImage, new List<string>());
            stdout.Should().StartWith("\nHello from Docker!");
            stderr.Should().BeEmpty();
        }
    }
}