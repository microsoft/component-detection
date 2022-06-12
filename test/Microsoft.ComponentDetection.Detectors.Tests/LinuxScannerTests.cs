using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class LinuxScannerTests
    {
        private const string SyftOutput = @"{
                ""distro"": {
                    ""id"":""test-distribution"",
                    ""versionId"":""1.0.0""
                },
                ""artifacts"": [
                    {
                        ""name"":""test"",
                        ""version"":""1.0.0"",
                        ""type"":""deb"",
                        ""locations"": [
                            {
                                ""path"": ""/var/lib/dpkg/status"",
                                ""layerID"": ""sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971""
                            }
                        ]
                    }
                ]
            }";
        
        private LinuxScanner linuxScanner;
        private Mock<IDockerService> mockDockerService;

        [TestInitialize]
        public void TestInitialize()
        {
            mockDockerService = new Mock<IDockerService>();
            mockDockerService.Setup(service => service.CanPingDockerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockDockerService.Setup(service => service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));

            linuxScanner = new LinuxScanner { DockerService = mockDockerService.Object };
        }

        [TestMethod]
        public async Task Should_ParseAlpine()
        {
            var syftOutput = await ResourceUtilities.LoadTextAsync(Path.Join("linux", "alpine.syft.json"));
            mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((syftOutput, string.Empty));
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:24302eb7d9085da80f016e7e4ae55417e412fb7e0a8021e95e3b60c67cde557d" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(14);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "libssl1.1");
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.SourceName == "openssl");
        }
        
        [TestMethod]
        public async Task Should_ParseMariner()
        {
            var syftOutput = await ResourceUtilities.LoadTextAsync(Path.Join("linux", "mariner.syft.json"));
            mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((syftOutput, string.Empty));
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:364ee232b5fcd1798d4caacdd604a9ecd4810a64df3845a5d7ded87778206fc7" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(67);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "openssl");
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.SourceName == "ca-certificates");
        }
        
        [TestMethod]
        public async Task Should_ParseUbuntu()
        {
            var syftOutput = await ResourceUtilities.LoadTextAsync(Path.Join("linux", "ubuntu.syft.json"));
            mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((syftOutput, string.Empty));
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:af7ed92504ae4c20128a0f01048d41d467fef5c795c38d0defdb998a187ed1d4" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(92);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "libgnutls30");
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.SourceName == "nettle");
        }
    }
}
