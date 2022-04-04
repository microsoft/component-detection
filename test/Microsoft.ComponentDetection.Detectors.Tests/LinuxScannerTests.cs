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
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:a1c01e366b99afb656cec4b16561b6ab299fa471011b4414826407af3a5884f8" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(14);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "openssl");
        }
        
        [TestMethod]
        public async Task Should_ParseMariner()
        {
            var syftOutput = await ResourceUtilities.LoadTextAsync(Path.Join("linux", "mariner.syft.json"));
            mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((syftOutput, string.Empty));
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:09951eca1ed8c40810aa3e6995a344827e21fbd06b74f35325a59b605d672764" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(103);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "openssl");
        }
        
        [TestMethod]
        public async Task Should_ParseUbuntu()
        {
            var syftOutput = await ResourceUtilities.LoadTextAsync(Path.Join("linux", "ubuntu.syft.json"));
            mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((syftOutput, string.Empty));
            
            var result = await linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:9f54eef412758095c8079ac465d494a2872e02e90bf1fb5f12a1641c0d1bb78b" } }, 0);

            result.Should().HaveCount(1);
            var linuxComponents = result.First().LinuxComponents;
            linuxComponents.Should().HaveCount(92);
            linuxComponents.Should().Contain(linuxComponent => linuxComponent.Name == "gnutls28");
        }
    }
}
