#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Linux.Factories;
using Microsoft.ComponentDetection.Detectors.Linux.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LinuxScannerTests
{
    private const string SyftOutputLicensesFieldAndAuthor =
        @"{
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

    private const string SyftOutputLicenseFieldAndMaintainer =
        @"{
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

    private const string SyftOutputNoAuthorOrLicense =
        @"{
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

    private const string SyftOutputIgnoreInvalidMarinerPackages =
        @"{
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

    private const string SyftOutputRemoveNonduplicatedMarinerPackages =
        @"{
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
    private readonly List<IArtifactComponentFactory> componentFactories;
    private readonly List<IArtifactFilter> artifactFilters;

    public LinuxScannerTests()
    {
        this.mockDockerService = new Mock<IDockerService>();
        this.mockDockerService.Setup(service =>
                service.CanPingDockerAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        this.mockDockerService.Setup(service =>
            service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
        );

        this.mockLogger = new Mock<ILogger<LinuxScanner>>();

        // Set up factories and filters
        this.componentFactories =
        [
            new LinuxComponentFactory(),
            new NpmComponentFactory(),
            new PipComponentFactory(),
        ];

        this.artifactFilters = [new Mariner2ArtifactFilter()];

        this.linuxScanner = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
    }

    [TestMethod]
    [DataRow(SyftOutputLicensesFieldAndAuthor)]
    [DataRow(SyftOutputLicenseFieldAndMaintainer)]
    public async Task TestLinuxScannerAsync(string syftOutput)
    {
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };
        var result = (
            await this.linuxScanner.ScanLinuxAsync(
                "fake_hash",
                [
                    new DockerLayer
                    {
                        LayerIndex = 0,
                        DiffId =
                            "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
                    },
                ],
                0,
                enabledTypes
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        var package = result.First() as LinuxComponent;
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
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };
        var result = (
            await this.linuxScanner.ScanLinuxAsync(
                "fake_hash",
                [
                    new DockerLayer
                    {
                        LayerIndex = 0,
                        DiffId =
                            "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
                    },
                ],
                0,
                enabledTypes
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        var package = result.First() as LinuxComponent;
        package.Name.Should().Be("test");
        package.Version.Should().Be("1.0.0");
        package.Release.Should().Be("1.0.0");
        package.Distribution.Should().Be("test-distribution");
        package.Author.Should().Be(null);
        package.License.Should().Be(null);
    }

    [TestMethod]
    [DataRow(SyftOutputIgnoreInvalidMarinerPackages)]
    public async Task TestLinuxScanner_SyftOutputIgnoreInvalidMarinerPackages_Async(
        string syftOutput
    )
    {
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };
        var result = (
            await this.linuxScanner.ScanLinuxAsync(
                "fake_hash",
                [
                    new DockerLayer
                    {
                        LayerIndex = 0,
                        DiffId =
                            "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6",
                    },
                ],
                0,
                enabledTypes
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        var package = result.First() as LinuxComponent;
        package.Name.Should().Be("busybox");
        package.Version.Should().Be("1.35.0-13.cm2");
        package.Release.Should().Be("2.0");
        package.Distribution.Should().Be("mariner");
        package.Author.Should().Be(null);
        package.License.Should().Be(null);
    }

    [TestMethod]
    [DataRow(SyftOutputRemoveNonduplicatedMarinerPackages)]
    public async Task TestLinuxScanner_SyftOutputKeepNonduplicatedMarinerPackages_Async(
        string syftOutput
    )
    {
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };
        var result = (
            await this.linuxScanner.ScanLinuxAsync(
                "fake_hash",
                [
                    new DockerLayer
                    {
                        LayerIndex = 0,
                        DiffId =
                            "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6",
                    },
                ],
                0,
                enabledTypes
            )
        )
            .First()
            .Components;

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestLinuxScanner_SupportsMultipleComponentTypes_Async()
    {
        const string syftOutputWithMixedTypes =
            @"{
                ""distro"": {
                    ""id"":""ubuntu"",
                    ""versionId"":""22.04""
                },
                ""artifacts"": [
                    {
                        ""name"":""curl"",
                        ""version"":""7.81.0-1ubuntu1.10"",
                        ""type"":""deb"",
                        ""locations"": [
                            {
                                ""path"": ""/var/lib/dpkg/status"",
                                ""layerID"": ""sha256:layer1""
                            }
                        ],
                        ""metadata"": {
                            ""maintainer"": ""Ubuntu Developers""
                        }
                    },
                    {
                        ""name"":""express"",
                        ""version"":""4.18.2"",
                        ""type"":""npm"",
                        ""locations"": [
                            {
                                ""path"": ""/app/node_modules/express/package.json"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ],
                        ""metadata"": {
                            ""author"": ""TJ Holowaychuk"",
                            ""integrity"": ""sha512-5/PsL6iGPdfQ/lKM1UuielYgv3BUoJfz1aUwU9vHZ+J7gyvwdQXFEBIEIaxeGf0GIcreATNyBExtalisDbuMqQ==""
                        }
                    },
                    {
                        ""name"":""requests"",
                        ""version"":""2.31.0"",
                        ""type"":""python"",
                        ""locations"": [
                            {
                                ""path"": ""/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ],
                        ""metadata"": {
                            ""author"": ""Kenneth Reitz"",
                            ""license"": ""Apache-2.0""
                        }
                    }
                ]
            }";

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithMixedTypes, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };
        var layers = await this.linuxScanner.ScanLinuxAsync(
            "fake_hash",
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes
        );

        var allComponents = layers.SelectMany(l => l.Components).ToList();
        allComponents.Should().HaveCount(3);

        // Verify Linux component
        var linuxComponent = allComponents.OfType<LinuxComponent>().Single();
        linuxComponent.Name.Should().Be("curl");
        linuxComponent.Version.Should().Be("7.81.0-1ubuntu1.10");
        linuxComponent.Distribution.Should().Be("ubuntu");

        // Verify Npm component
        var npmComponent = allComponents.OfType<NpmComponent>().Single();
        npmComponent.Name.Should().Be("express");
        npmComponent.Version.Should().Be("4.18.2");
        npmComponent
            .Hash.Should()
            .Be(
                "sha512-5/PsL6iGPdfQ/lKM1UuielYgv3BUoJfz1aUwU9vHZ+J7gyvwdQXFEBIEIaxeGf0GIcreATNyBExtalisDbuMqQ=="
            );

        // Verify Pip component
        var pipComponent = allComponents.OfType<PipComponent>().Single();
        pipComponent.Name.Should().Be("requests");
        pipComponent.Version.Should().Be("2.31.0");
        pipComponent.License.Should().Be("Apache-2.0");
    }

    [TestMethod]
    public async Task TestLinuxScanner_FiltersComponentsByEnabledTypes_OnlyLinux_Async()
    {
        const string syftOutputWithMixedTypes =
            @"{
                ""distro"": {
                    ""id"":""ubuntu"",
                    ""versionId"":""22.04""
                },
                ""artifacts"": [
                    {
                        ""name"":""curl"",
                        ""version"":""7.81.0-1ubuntu1.10"",
                        ""type"":""deb"",
                        ""locations"": [
                            {
                                ""path"": ""/var/lib/dpkg/status"",
                                ""layerID"": ""sha256:layer1""
                            }
                        ],
                        ""metadata"": {
                            ""maintainer"": ""Ubuntu Developers""
                        }
                    },
                    {
                        ""name"":""express"",
                        ""version"":""4.18.2"",
                        ""type"":""npm"",
                        ""locations"": [
                            {
                                ""path"": ""/app/node_modules/express/package.json"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ],
                        ""metadata"": {
                            ""author"": ""TJ Holowaychuk""
                        }
                    },
                    {
                        ""name"":""requests"",
                        ""version"":""2.31.0"",
                        ""type"":""python"",
                        ""locations"": [
                            {
                                ""path"": ""/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ]
                    }
                ]
            }";

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithMixedTypes, string.Empty));

        // Only enable Linux component type
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        var layers = await this.linuxScanner.ScanLinuxAsync(
            "fake_hash",
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes
        );

        var allComponents = layers.SelectMany(l => l.Components).ToList();

        // Should only have the Linux (deb) component, npm and pip should be filtered out
        allComponents.Should().ContainSingle();
        allComponents.Should().AllBeOfType<LinuxComponent>();

        var linuxComponent = allComponents.OfType<LinuxComponent>().Single();
        linuxComponent.Name.Should().Be("curl");
        linuxComponent.Version.Should().Be("7.81.0-1ubuntu1.10");
    }

    [TestMethod]
    public async Task TestLinuxScanner_FiltersComponentsByEnabledTypes_OnlyNpmAndPip_Async()
    {
        const string syftOutputWithMixedTypes =
            @"{
                ""distro"": {
                    ""id"":""ubuntu"",
                    ""versionId"":""22.04""
                },
                ""artifacts"": [
                    {
                        ""name"":""curl"",
                        ""version"":""7.81.0-1ubuntu1.10"",
                        ""type"":""deb"",
                        ""locations"": [
                            {
                                ""path"": ""/var/lib/dpkg/status"",
                                ""layerID"": ""sha256:layer1""
                            }
                        ],
                        ""metadata"": {
                            ""maintainer"": ""Ubuntu Developers""
                        }
                    },
                    {
                        ""name"":""express"",
                        ""version"":""4.18.2"",
                        ""type"":""npm"",
                        ""locations"": [
                            {
                                ""path"": ""/app/node_modules/express/package.json"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ],
                        ""metadata"": {
                            ""author"": ""TJ Holowaychuk""
                        }
                    },
                    {
                        ""name"":""requests"",
                        ""version"":""2.31.0"",
                        ""type"":""python"",
                        ""locations"": [
                            {
                                ""path"": ""/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA"",
                                ""layerID"": ""sha256:layer2""
                            }
                        ]
                    }
                ]
            }";

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithMixedTypes, string.Empty));

        // Only enable Npm and Pip component types (exclude Linux)
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Npm, ComponentType.Pip };
        var layers = await this.linuxScanner.ScanLinuxAsync(
            "fake_hash",
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes
        );

        var allComponents = layers.SelectMany(l => l.Components).ToList();

        // Should only have npm and pip components, Linux component should be filtered out
        allComponents.Should().HaveCount(2);
        allComponents.Should().NotContain(c => c is LinuxComponent);

        var npmComponent = allComponents.OfType<NpmComponent>().Single();
        npmComponent.Name.Should().Be("express");

        var pipComponent = allComponents.OfType<PipComponent>().Single();
        pipComponent.Name.Should().Be("requests");
    }
}
