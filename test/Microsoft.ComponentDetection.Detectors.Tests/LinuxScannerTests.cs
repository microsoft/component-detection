#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LinuxScannerTests
{
    private const string SyftOutputLicensesFieldAndAuthor = @"{
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
                        ],
                        ""metadata"": {
                            ""author"": ""John Doe""
                        },
                        ""licenses"": [
                            {
                                ""value"": ""MIT"",
                            },
                            {
                                ""value"": ""GPLv2"",
                            },
                            {
                                ""value"": ""GPLv3"",
                            }
                        ]
                    }
                ]
            }";

    private const string SyftOutputLicenseFieldAndMaintainer = @"{
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
                        ],
                        ""metadata"": {
                            ""maintainer"": ""John Doe"",
                            ""license"": ""MIT, GPLv2, GPLv3""
                        }
                    }
                ]
            }";

    private const string SyftOutputNoAuthorOrLicense = @"{
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
                        ],
                    }
                ]
            }";

    private const string SyftOutputIgnoreInvalidMarinerPackages = @"{
                ""distro"": {
                    ""prettyName"": ""CBL-Mariner/Linux"",
                    ""name"": ""Common Base Linux Mariner"",
                    ""id"": ""mariner"",
                    ""version"": ""2.0.20250304"",
                    ""versionID"": ""2.0"",
                },
                ""artifacts"": [
                    {
                        ""id"": ""4af20256df269904"",
                        ""name"": ""busybox"",
                        ""version"": ""1.35.0"",
                        ""type"": ""rpm"",
                        ""foundBy"": ""elf-binary-package-cataloger"",
                        ""locations"": [
                            {
                                ""path"": ""/usr/sbin/busybox"",
                                ""layerID"": ""sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6"",
                                ""accessPath"": ""/usr/sbin/busybox"",
                                ""annotations"": { ""evidence"": ""primary"" }
                            }
                        ],
                        ""cpes"": [
                            {
                                ""cpe"": ""cpe:2.3:a:busybox:busybox:1.35.0:*:*:*:*:*:*:*"",
                                ""source"": ""syft-generated""
                            }
                        ],
                        ""purl"": ""pkg:rpm/mariner/busybox@1.35.0?distro=mariner-2.0"",
                        ""metadataType"": ""elf-binary-package-note-json-payload"",
                        ""metadata"": { ""type"": ""rpm"", ""os"": ""mariner"", ""osVersion"": ""2.0"" }
                    },
                    {
                        ""id"": ""45849b2d67d236b0"",
                        ""name"": ""busybox"",
                        ""version"": ""1.35.0-13.cm2"",
                        ""type"": ""rpm"",
                        ""foundBy"": ""rpm-db-cataloger"",
                        ""locations"": [
                            {
                                ""path"": ""/var/lib/rpmmanifest/container-manifest-2"",
                                ""layerID"": ""sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6"",
                                ""accessPath"": ""/var/lib/rpmmanifest/container-manifest-2"",
                                ""annotations"": { ""evidence"": ""primary"" }
                            }
                        ],
                        ""cpes"": [
                            {
                                ""cpe"": ""cpe:2.3:a:microsoftcorporation:busybox:1.35.0-13.cm2:*:*:*:*:*:*:*"",
                                ""source"": ""syft-generated""
                            },
                            {
                                ""cpe"": ""cpe:2.3:a:busybox:busybox:1.35.0-13.cm2:*:*:*:*:*:*:*"",
                                ""source"": ""syft-generated""
                            }
                        ],
                        ""purl"": ""pkg:rpm/busybox@1.35.0-13.cm2?arch=x86_64&upstream=busybox-1.35.0-13.cm2.src.rpm"",
                        ""metadataType"": ""rpm-db-entry"",
                        ""metadata"": {
                            ""name"": ""busybox"",
                            ""version"": ""1.35.0"",
                            ""epoch"": null,
                            ""architecture"": ""x86_64"",
                            ""release"": ""13.cm2"",
                            ""sourceRpm"": ""busybox-1.35.0-13.cm2.src.rpm"",
                            ""size"": 3512551,
                            ""vendor"": ""Microsoft Corporation"",
                            ""files"": null
                        }
                    },
                ]
            }";

    private const string SyftOutputRemoveNonduplicatedMarinerPackages = @"{
                ""distro"": {
                    ""prettyName"": ""CBL-Mariner/Linux"",
                    ""name"": ""Common Base Linux Mariner"",
                    ""id"": ""mariner"",
                    ""version"": ""2.0.20250304"",
                    ""versionID"": ""2.0"",
                },
                ""artifacts"": [
                    {
                        ""id"": ""4af20256df269904"",
                        ""name"": ""busybox"",
                        ""version"": ""1.35.0"",
                        ""type"": ""rpm"",
                        ""foundBy"": ""elf-binary-package-cataloger"",
                        ""locations"": [
                            {
                                ""path"": ""/usr/sbin/busybox"",
                                ""layerID"": ""sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6"",
                                ""accessPath"": ""/usr/sbin/busybox"",
                                ""annotations"": { ""evidence"": ""primary"" }
                            }
                        ],
                        ""cpes"": [
                            {
                                ""cpe"": ""cpe:2.3:a:busybox:busybox:1.35.0:*:*:*:*:*:*:*"",
                                ""source"": ""syft-generated""
                            }
                        ],
                        ""purl"": ""pkg:rpm/mariner/busybox@1.35.0?distro=mariner-2.0"",
                        ""metadataType"": ""elf-binary-package-note-json-payload"",
                        ""metadata"": { ""type"": ""rpm"", ""os"": ""mariner"", ""osVersion"": ""2.0"" }
                    },
                ]
            }";

    private readonly LinuxScanner linuxScanner;
    private readonly Mock<IDockerService> mockDockerService;
    private readonly Mock<ILogger<LinuxScanner>> mockLogger;

    public LinuxScannerTests()
    {
        this.mockDockerService = new Mock<IDockerService>();
        this.mockDockerService.Setup(service => service.CanPingDockerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockDockerService.Setup(service => service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));

        this.mockLogger = new Mock<ILogger<LinuxScanner>>();

        this.linuxScanner = new LinuxScanner(this.mockDockerService.Object, this.mockLogger.Object);
    }

    [TestMethod]
    [DataRow(SyftOutputLicensesFieldAndAuthor)]
    [DataRow(SyftOutputLicenseFieldAndMaintainer)]
    public async Task TestLinuxScannerAsync(string syftOutput)
    {
        this.mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((syftOutput, string.Empty));

        var result = (await this.linuxScanner.ScanLinuxAsync("fake_hash", [new DockerLayer { LayerIndex = 0, DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971" }], 0)).First().LinuxComponents;

        result.Should().ContainSingle();
        var package = result.First();
        package.Name.Should().Be("test");
        package.Version.Should().Be("1.0.0");
        package.Release.Should().Be("1.0.0");
        package.Distribution.Should().Be("test-distribution");
        package.Author.Should().Be("John Doe");
        package.License.Should().Be("MIT, GPLv2, GPLv3");
    }

    [TestMethod]
    [DataRow(SyftOutputNoAuthorOrLicense)]
    public async Task TestLinuxScanner_ReturnsNullAuthorAndLicense_Async(string syftOutput)
    {
        this.mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((syftOutput, string.Empty));

        var result = (await this.linuxScanner.ScanLinuxAsync("fake_hash", [new DockerLayer { LayerIndex = 0, DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971" }], 0)).First().LinuxComponents;

        result.Should().ContainSingle();
        var package = result.First();
        package.Name.Should().Be("test");
        package.Version.Should().Be("1.0.0");
        package.Release.Should().Be("1.0.0");
        package.Distribution.Should().Be("test-distribution");
        package.Author.Should().Be(null);
        package.License.Should().Be(null);
    }

    [TestMethod]
    [DataRow(SyftOutputIgnoreInvalidMarinerPackages)]
    public async Task TestLinuxScanner_SyftOutputIgnoreInvalidMarinerPackages_Async(string syftOutput)
    {
        this.mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((syftOutput, string.Empty));

        var result = (await this.linuxScanner.ScanLinuxAsync("fake_hash", [new DockerLayer { LayerIndex = 0, DiffId = "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6" }], 0)).First().LinuxComponents;

        result.Should().ContainSingle();
        var package = result.First();
        package.Name.Should().Be("busybox");
        package.Version.Should().Be("1.35.0-13.cm2");
        package.Release.Should().Be("2.0");
        package.Distribution.Should().Be("mariner");
        package.Author.Should().Be(null);
        package.License.Should().Be(null);
    }

    [TestMethod]
    [DataRow(SyftOutputRemoveNonduplicatedMarinerPackages)]
    public async Task TestLinuxScanner_SyftOutputKeepNonduplicatedMarinerPackages_Async(string syftOutput)
    {
        this.mockDockerService.Setup(service => service.CreateAndRunContainerAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((syftOutput, string.Empty));

        var result = (await this.linuxScanner.ScanLinuxAsync("fake_hash", [new DockerLayer { LayerIndex = 0, DiffId = "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6" }], 0)).First().LinuxComponents;

        result.Should().BeEmpty();
    }
}
