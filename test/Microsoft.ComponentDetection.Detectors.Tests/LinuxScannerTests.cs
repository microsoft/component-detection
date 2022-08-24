using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Detectors.Linux;
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
            this.mockDockerService = new Mock<IDockerService>();
            this.mockDockerService.Setup(service => service.CanPingDockerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            this.mockDockerService.Setup(service => service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
            this.mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SyftOutput, string.Empty));

            this.linuxScanner = new LinuxScanner { DockerService = this.mockDockerService.Object };
        }

        [TestMethod]
        public async Task TestLinuxScanner()
        {
            var result = (await this.linuxScanner.ScanLinuxAsync("fake_hash", new[] { new DockerLayer { LayerIndex = 0, DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971" } }, 0)).First().LinuxComponents;

            result.Should().HaveCount(1);
            var package = result.First();
            package.Name.Should().Be("test");
            package.Version.Should().Be("1.0.0");
            package.Release.Should().Be("1.0.0");
            package.Distribution.Should().Be("test-distribution");
        }
    }
}
