namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
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
    private const string SyftOutputLicensesFieldAndAuthor = """
        {
            "distro": {
                "id":"test-distribution",
                "versionID":"1.0.0"
            },
            "artifacts": [
                {
                    "name":"test",
                    "version":"1.0.0",
                    "type":"deb",
                    "locations": [
                        {
                            "path": "/var/lib/dpkg/status",
                            "layerID": "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971"
                        }
                    ],
                    "metadata": {
                        "author": "John Doe"
                    },
                    "licenses": [
                        {
                            "value": "MIT"
                        },
                        {
                            "value": "GPLv2"
                        },
                        {
                            "value": "GPLv3"
                        }
                    ]
                }
            ]
        }
        """;

    private const string SyftOutputLicenseFieldAndMaintainer = """
        {
            "distro": {
                "id":"test-distribution",
                "versionID":"1.0.0"
            },
            "artifacts": [
                {
                    "name":"test",
                    "version":"1.0.0",
                    "type":"deb",
                    "locations": [
                        {
                            "path": "/var/lib/dpkg/status",
                            "layerID": "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971"
                        }
                    ],
                    "metadata": {
                        "maintainer": "John Doe",
                        "license": "MIT, GPLv2, GPLv3"
                    }
                }
            ]
        }
        """;

    private const string SyftOutputNoAuthorOrLicense = """
        {
            "distro": {
                "id":"test-distribution",
                "versionID":"1.0.0"
            },
            "artifacts": [
                {
                    "name":"test",
                    "version":"1.0.0",
                    "type":"deb",
                    "locations": [
                        {
                            "path": "/usr/bin/test",
                            "layerID": "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971"
                        },
                        {
                            "path": "/var/lib/dpkg/status",
                            "layerID": "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971"
                        }
                    ]
                }
            ]
        }
        """;

    private const string SyftOutputIgnoreInvalidMarinerPackages = """
        {
            "distro": {
                "prettyName": "CBL-Mariner/Linux",
                "name": "Common Base Linux Mariner",
                "id": "mariner",
                "version": "2.0.20250304",
                "versionID": "2.0"
            },
            "artifacts": [
                {
                    "id": "4af20256df269904",
                    "name": "busybox",
                    "version": "1.35.0",
                    "type": "rpm",
                    "foundBy": "elf-binary-package-cataloger",
                    "locations": [
                        {
                            "path": "/usr/sbin/busybox",
                            "layerID": "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6",
                            "accessPath": "/usr/sbin/busybox",
                            "annotations": { "evidence": "primary" }
                        }
                    ],
                    "cpes": [
                        {
                            "cpe": "cpe:2.3:a:busybox:busybox:1.35.0:*:*:*:*:*:*:*",
                            "source": "syft-generated"
                        }
                    ],
                    "purl": "pkg:rpm/mariner/busybox@1.35.0?distro=mariner-2.0",
                    "metadataType": "elf-binary-package-note-json-payload",
                    "metadata": { "type": "rpm", "os": "mariner", "osVersion": "2.0" }
                },
                {
                    "id": "45849b2d67d236b0",
                    "name": "busybox",
                    "version": "1.35.0-13.cm2",
                    "type": "rpm",
                    "foundBy": "rpm-db-cataloger",
                    "locations": [
                        {
                            "path": "/var/lib/rpmmanifest/container-manifest-2",
                            "layerID": "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6",
                            "accessPath": "/var/lib/rpmmanifest/container-manifest-2",
                            "annotations": { "evidence": "primary" }
                        }
                    ],
                    "cpes": [
                        {
                            "cpe": "cpe:2.3:a:microsoftcorporation:busybox:1.35.0-13.cm2:*:*:*:*:*:*:*",
                            "source": "syft-generated"
                        },
                        {
                            "cpe": "cpe:2.3:a:busybox:busybox:1.35.0-13.cm2:*:*:*:*:*:*:*",
                            "source": "syft-generated"
                        }
                    ],
                    "purl": "pkg:rpm/busybox@1.35.0-13.cm2?arch=x86_64&upstream=busybox-1.35.0-13.cm2.src.rpm",
                    "metadataType": "rpm-db-entry",
                    "metadata": {
                        "name": "busybox",
                        "version": "1.35.0",
                        "epoch": null,
                        "architecture": "x86_64",
                        "release": "13.cm2",
                        "sourceRpm": "busybox-1.35.0-13.cm2.src.rpm",
                        "size": 3512551,
                        "vendor": "Microsoft Corporation",
                        "files": null
                    }
                }
            ]
        }
        """;

    private const string SyftOutputRemoveNonduplicatedMarinerPackages = """
        {
            "distro": {
                "prettyName": "CBL-Mariner/Linux",
                "name": "Common Base Linux Mariner",
                "id": "mariner",
                "version": "2.0.20250304",
                "versionID": "2.0"
            },
            "artifacts": [
                {
                    "id": "4af20256df269904",
                    "name": "busybox",
                    "version": "1.35.0",
                    "type": "rpm",
                    "foundBy": "elf-binary-package-cataloger",
                    "locations": [
                        {
                            "path": "/usr/sbin/busybox",
                            "layerID": "sha256:81caca2c07d9859b258a9cdfb1b1ab9d063f30ab73a4de9ea2ae760fd175bac6",
                            "accessPath": "/usr/sbin/busybox",
                            "annotations": { "evidence": "primary" }
                        }
                    ],
                    "cpes": [
                        {
                            "cpe": "cpe:2.3:a:busybox:busybox:1.35.0:*:*:*:*:*:*:*",
                            "source": "syft-generated"
                        }
                    ],
                    "purl": "pkg:rpm/mariner/busybox@1.35.0?distro=mariner-2.0",
                    "metadataType": "elf-binary-package-note-json-payload",
                    "metadata": { "type": "rpm", "os": "mariner", "osVersion": "2.0" }
                }
            ]
        }
        """;

    private readonly LinuxScanner linuxScanner;
    private readonly Mock<IDockerService> mockDockerService;
    private readonly Mock<ILogger<LinuxScanner>> mockLogger;
    private readonly List<IArtifactComponentFactory> componentFactories;
    private readonly List<IArtifactFilter> artifactFilters;

    public LinuxScannerTests()
    {
        // Clear the static syft run cache to prevent cross-test interference.
        LinuxScanner.ResetCache();

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
                    It.IsAny<IList<string>>(),
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
                enabledTypes,
                LinuxScannerScope.AllLayers
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        result.First().Should().BeOfType<LinuxComponent>();
        var package = result.First() as LinuxComponent;
        package.Should().NotBeNull();
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
                    It.IsAny<IList<string>>(),
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
                enabledTypes,
                LinuxScannerScope.AllLayers
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        result.First().Should().BeOfType<LinuxComponent>();
        var package = result.First() as LinuxComponent;
        package.Should().NotBeNull();
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
                    It.IsAny<IList<string>>(),
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
                enabledTypes,
                LinuxScannerScope.AllLayers
            )
        )
            .First()
            .Components;

        result.Should().ContainSingle();
        result.First().Should().BeOfType<LinuxComponent>();
        var package = result.First() as LinuxComponent;
        package.Should().NotBeNull();
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
                    It.IsAny<IList<string>>(),
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
                enabledTypes,
                LinuxScannerScope.AllLayers
            )
        )
            .First()
            .Components;

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestLinuxScanner_SupportsMultipleComponentTypes_Async()
    {
        const string syftOutputWithMixedTypes = """
            {
                "distro": {
                    "id":"ubuntu",
                    "versionID":"22.04"
                },
                "artifacts": [
                    {
                        "name":"curl",
                        "version":"7.81.0-1ubuntu1.10",
                        "type":"deb",
                        "locations": [
                            {
                                "path": "/var/lib/dpkg/status",
                                "layerID": "sha256:layer1"
                            }
                        ],
                        "metadata": {
                            "maintainer": "Ubuntu Developers"
                        }
                    },
                    {
                        "name":"express",
                        "version":"4.18.2",
                        "type":"npm",
                        "locations": [
                            {
                                "path": "/app/node_modules/express/package.json",
                                "layerID": "sha256:layer2"
                            }
                        ],
                        "metadata": {
                            "author": "TJ Holowaychuk",
                            "integrity": "sha512-5/PsL6iGPdfQ/lKM1UuielYgv3BUoJfz1aUwU9vHZ+J7gyvwdQXFEBIEIaxeGf0GIcreATNyBExtalisDbuMqQ=="
                        }
                    },
                    {
                        "name":"requests",
                        "version":"2.31.0",
                        "type":"python",
                        "locations": [
                            {
                                "path": "/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA",
                                "layerID": "sha256:layer2"
                            }
                        ],
                        "metadata": {
                            "author": "Kenneth Reitz",
                            "license": "Apache-2.0"
                        }
                    }
                ]
            }
            """;

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
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
            enabledTypes,
            LinuxScannerScope.AllLayers
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
        const string syftOutputWithMixedTypes = """
            {
                "distro": {
                    "id":"ubuntu",
                    "versionID":"22.04"
                },
                "artifacts": [
                    {
                        "name":"curl",
                        "version":"7.81.0-1ubuntu1.10",
                        "type":"deb",
                        "locations": [
                            {
                                "path": "/var/lib/dpkg/status",
                                "layerID": "sha256:layer1"
                            }
                        ],
                        "metadata": {
                            "maintainer": "Ubuntu Developers"
                        }
                    },
                    {
                        "name":"express",
                        "version":"4.18.2",
                        "type":"npm",
                        "locations": [
                            {
                                "path": "/app/node_modules/express/package.json",
                                "layerID": "sha256:layer2"
                            }
                        ],
                        "metadata": {
                            "author": "TJ Holowaychuk"
                        }
                    },
                    {
                        "name":"requests",
                        "version":"2.31.0",
                        "type":"python",
                        "locations": [
                            {
                                "path": "/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA",
                                "layerID": "sha256:layer2"
                            }
                        ]
                    }
                ]
            }
            """;

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
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
            enabledTypes,
            LinuxScannerScope.AllLayers
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
        const string syftOutputWithMixedTypes = """
            {
                "distro": {
                    "id":"ubuntu",
                    "versionId":"22.04"
                },
                "artifacts": [
                    {
                        "name":"curl",
                        "version":"7.81.0-1ubuntu1.10",
                        "type":"deb",
                        "locations": [
                            {
                                "path": "/var/lib/dpkg/status",
                                "layerID": "sha256:layer1"
                            }
                        ],
                        "metadata": {
                            "maintainer": "Ubuntu Developers"
                        }
                    },
                    {
                        "name":"express",
                        "version":"4.18.2",
                        "type":"npm",
                        "locations": [
                            {
                                "path": "/app/node_modules/express/package.json",
                                "layerID": "sha256:layer2"
                            }
                        ],
                        "metadata": {
                            "author": "TJ Holowaychuk"
                        }
                    },
                    {
                        "name":"requests",
                        "version":"2.31.0",
                        "type":"python",
                        "locations": [
                            {
                                "path": "/usr/local/lib/python3.10/site-packages/requests-2.31.0.dist-info/METADATA",
                                "layerID": "sha256:layer2"
                            }
                        ]
                    }
                ]
            }
            """;

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
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
            enabledTypes,
            LinuxScannerScope.AllLayers
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

    [TestMethod]
    [DataRow(LinuxScannerScope.AllLayers, "all-layers")]
    [DataRow(LinuxScannerScope.Squashed, "squashed")]
    public async Task TestLinuxScanner_ScopeParameter_IncludesCorrectFlagAsync(
        LinuxScannerScope scope,
        string expectedFlag
    )
    {
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SyftOutputNoAuthorOrLicense, string.Empty));

        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        await this.linuxScanner.ScanLinuxAsync(
            "fake_hash",
            [new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" }],
            0,
            enabledTypes,
            scope
        );

        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.Is<List<string>>(cmd =>
                        cmd.Contains("--scope") && cmd.Contains(expectedFlag)
                    ),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_InvalidScopeParameter_ThrowsArgumentOutOfRangeExceptionAsync()
    {
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        var invalidScope = (LinuxScannerScope)999; // Invalid enum value

        Func<Task> action = async () =>
            await this.linuxScanner.ScanLinuxAsync(
                "fake_hash",
                [new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" }],
                0,
                enabledTypes,
                invalidScope
            );

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public async Task TestLinuxScanner_ScanLinuxSyftOutputAsync_ReturnsParsedSyftOutputAsync()
    {
        const string syftOutputWithSource = """
            {
                "distro": {
                    "id": "azurelinux",
                    "versionID": "3.0"
                },
                "artifacts": [
                    {
                        "name": "bash",
                        "version": "5.2.15-3.azl3",
                        "type": "rpm",
                        "locations": [
                            {
                                "path": "/var/lib/rpm/Packages",
                                "layerID": "sha256:aaa111"
                            }
                        ],
                        "metadata": {},
                        "licenses": [
                            { "value": "GPL-3.0-or-later" }
                        ]
                    }
                ],
                "source": {
                    "id": "sha256:abc123",
                    "name": "/oci-image",
                    "type": "image",
                    "version": "sha256:abc123",
                    "metadata": {
                        "userInput": "/oci-image",
                        "imageID": "sha256:image123",
                        "manifestDigest": "sha256:abc123",
                        "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
                        "tags": ["myregistry.io/myimage:latest"],
                        "imageSize": 100000,
                        "layers": [
                            {
                                "mediaType": "application/vnd.docker.image.rootfs.diff.tar.gzip",
                                "digest": "sha256:aaa111",
                                "size": 50000
                            },
                            {
                                "mediaType": "application/vnd.docker.image.rootfs.diff.tar.gzip",
                                "digest": "sha256:bbb222",
                                "size": 50000
                            }
                        ],
                        "repoDigests": [],
                        "architecture": "amd64",
                        "os": "linux",
                        "labels": {
                            "image.base.ref.name": "mcr.microsoft.com/azurelinux/base/core:3.0",
                            "image.base.digest": "sha256:basedigest123"
                        }
                    }
                }
            }
            """;

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithSource, string.Empty));

        var additionalBinds = new List<string> { "/some/oci/path:/oci-image:ro" };
        var syftOutput = await this.linuxScanner.GetSyftOutputAsync(
            "oci-dir:/oci-image",
            additionalBinds,
            LinuxScannerScope.AllLayers
        );

        syftOutput.Should().NotBeNull();
        syftOutput.Artifacts.Should().ContainSingle();
        syftOutput.Artifacts[0].Name.Should().Be("bash");

        // Verify source metadata can be extracted
        var sourceMetadata = syftOutput.Source?.GetSyftSourceMetadata();
        sourceMetadata.Should().NotBeNull();
        sourceMetadata.ImageId.Should().Be("sha256:image123");
        sourceMetadata.Tags.Should().ContainSingle().Which.Should().Be("myregistry.io/myimage:latest");
        sourceMetadata.Layers.Should().HaveCount(2);
        sourceMetadata.Labels.Should().ContainKey("image.base.ref.name");

        // Verify ProcessSyftOutput works with the returned output
        var containerLayers = sourceMetadata.Layers
            .Select((layer, index) => new DockerLayer { DiffId = layer.Digest, LayerIndex = index })
            .ToList();
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        var layerMappedComponents = this.linuxScanner.ProcessSyftOutput(
            syftOutput, containerLayers, enabledTypes);

        layerMappedComponents.Should().HaveCount(2);
        var layerWithComponents = layerMappedComponents
            .First(l => l.DockerLayer.DiffId == "sha256:aaa111");
        layerWithComponents.Components.Should().ContainSingle();
        layerWithComponents.Components.First().Should().BeOfType<LinuxComponent>();
        var bashComponent = layerWithComponents.Components.First() as LinuxComponent;
        bashComponent.Should().NotBeNull();
        bashComponent.Name.Should().Be("bash");
        bashComponent.Version.Should().Be("5.2.15-3.azl3");
        bashComponent.Distribution.Should().Be("azurelinux");
    }

    [TestMethod]
    public async Task TestLinuxScanner_ScanLinuxSyftOutputAsync_PassesAdditionalBindsAndCommandAsync()
    {
        const string syftOutput = """
            {
                "distro": { "id": "test", "versionID": "1.0" },
                "artifacts": [],
                "source": {
                    "id": "sha256:abc",
                    "name": "/oci-image",
                    "type": "image",
                    "version": "sha256:abc",
                    "metadata": {
                        "userInput": "/oci-image",
                        "imageID": "sha256:img",
                        "layers": [],
                        "labels": {}
                    }
                }
            }
            """;

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var additionalBinds = new List<string> { "/host/path/to/oci:/oci-image:ro" };
        await this.linuxScanner.GetSyftOutputAsync(
            "oci-dir:/oci-image",
            additionalBinds,
            LinuxScannerScope.AllLayers
        );

        // Verify the Syft command uses oci-dir: scheme and passes binds
        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.Is<List<string>>(cmd => cmd[0] == "oci-dir:/oci-image"),
                    It.Is<IList<string>>(binds =>
                        binds.Count == 1 && binds[0] == "/host/path/to/oci:/oci-image:ro"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public void TestLinuxScanner_ProcessSyftOutput_ReturnsComponentsWithoutLayerInfoWhenNoContainerLayers()
    {
        var syftOutputJson = """
            {
                "distro": { "id": "azurelinux", "versionID": "3.0" },
                "artifacts": [
                    {
                        "name": "bash",
                        "version": "5.2.15",
                        "type": "rpm",
                        "locations": [
                            {
                                "path": "/var/lib/rpm/rpmdb.sqlite",
                                "layerID": "sha256:layer1"
                            }
                        ]
                    },
                    {
                        "name": "openssl",
                        "version": "3.1.0",
                        "type": "rpm",
                        "locations": [
                            {
                                "path": "/var/lib/rpm/rpmdb.sqlite",
                                "layerID": "sha256:layer2"
                            }
                        ]
                    }
                ],
                "source": {
                    "id": "sha256:abc",
                    "name": "/oci-image",
                    "type": "image",
                    "version": "sha256:abc"
                }
            }
            """;
        var syftOutput = SyftOutput.FromJson(syftOutputJson);
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };

        // Pass empty container layers — components should still be returned
        var result = this.linuxScanner.ProcessSyftOutput(
            syftOutput, [], enabledTypes).ToList();

        // All components should be grouped under a single entry with no layer info
        result.Should().ContainSingle();

        var entry = result.First();
        entry.DockerLayer.Should().NotBeNull();
        entry.DockerLayer.DiffId.Should().Be(string.Empty);
        entry.DockerLayer.LayerIndex.Should().Be(0);
        entry.DockerLayer.IsBaseImage.Should().BeFalse();

        entry.Components.Should().HaveCount(2);
        entry.Components.Should().AllBeOfType<LinuxComponent>();
        entry.Components.Select(c => (c as LinuxComponent)!.Name)
            .Should().Contain("bash").And.Contain("openssl");
    }

    [TestMethod]
    public async Task TestLinuxScanner_ConcurrentScansSameImage_RunsSyftOnlyOnceAsync()
    {
        LinuxScanner.ResetCache();

        // Use a TCS so the mock doesn't complete synchronously — both callers
        // must enter GetOrAdd while the task is still in-flight.
        var syftTcs = new TaskCompletionSource<(string, string)>();

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(syftTcs.Task);

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };

        var layers = new[]
        {
            new DockerLayer
            {
                LayerIndex = 0,
                DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
            },
        };

        var scanner1 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
        var scanner2 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );

        // Both start while the task is still pending — they should share one run.
        var task1 = scanner1.ScanLinuxAsync("same_hash", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);
        var task2 = scanner2.ScanLinuxAsync("same_hash", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);

        // Complete the single syft run.
        syftTcs.SetResult((SyftOutputNoAuthorOrLicense, string.Empty));

        var results = await Task.WhenAll(task1, task2);

        results[0].Should().NotBeEmpty();
        results[1].Should().NotBeEmpty();
        results[0].First().Components.Should().ContainSingle();
        results[1].First().Components.Should().ContainSingle();

        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_ConcurrentScansDifferentImages_RunsSyftForEachAsync()
    {
        LinuxScanner.ResetCache();

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SyftOutputNoAuthorOrLicense, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };

        var layers = new[]
        {
            new DockerLayer
            {
                LayerIndex = 0,
                DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
            },
        };

        var scanner1 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
        var scanner2 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );

        var task1 = scanner1.ScanLinuxAsync("image_hash_A", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);
        var task2 = scanner2.ScanLinuxAsync("image_hash_B", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);

        var results = await Task.WhenAll(task1, task2);

        results[0].Should().NotBeEmpty();
        results[1].Should().NotBeEmpty();

        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_ConcurrentScansDifferentScopes_RunsSyftForEachAsync()
    {
        LinuxScanner.ResetCache();

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SyftOutputNoAuthorOrLicense, string.Empty));

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };

        var layers = new[]
        {
            new DockerLayer
            {
                LayerIndex = 0,
                DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
            },
        };

        var scanner1 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
        var scanner2 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );

        var task1 = scanner1.ScanLinuxAsync("same_hash", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);
        var task2 = scanner2.ScanLinuxAsync("same_hash", layers, 0, enabledTypes, LinuxScannerScope.Squashed);

        var results = await Task.WhenAll(task1, task2);

        results[0].Should().NotBeEmpty();
        results[1].Should().NotBeEmpty();

        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_SyftCacheKey_BindOrderDoesNotMatterAsync()
    {
        LinuxScanner.ResetCache();

        const string syftOutputWithSource = """
            {
                "distro": { "id": "test", "versionID": "1.0" },
                "artifacts": [],
                "source": {
                    "id": "sha256:abc",
                    "name": "/img",
                    "type": "image",
                    "version": "sha256:abc",
                    "metadata": {
                        "userInput": "/img",
                        "imageID": "sha256:img",
                        "layers": [],
                        "labels": {}
                    }
                }
            }
            """;

        // Use a TCS so the mock doesn't complete synchronously — both callers
        // must enter GetOrAdd while the task is still in-flight.
        var syftTcs = new TaskCompletionSource<(string, string)>();

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(syftTcs.Task);

        var scanner1 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
        var scanner2 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );

        // Both calls start concurrently with binds in different order.
        var task1 = scanner1.GetSyftOutputAsync(
            "oci-dir:/img",
            ["/host/a:/container/a:ro", "/host/b:/container/b:ro"],
            LinuxScannerScope.AllLayers
        );
        var task2 = scanner2.GetSyftOutputAsync(
            "oci-dir:/img",
            ["/host/b:/container/b:ro", "/host/a:/container/a:ro"],
            LinuxScannerScope.AllLayers
        );

        // Complete the single syft run.
        syftTcs.SetResult((syftOutputWithSource, string.Empty));

        var results = await Task.WhenAll(task1, task2);

        results[0].Should().NotBeNull();
        results[1].Should().NotBeNull();

        // Bind order shouldn't matter — both should share a single container run.
        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_FailedSyftRun_RemovesCacheEntry_AllowsRetryAsync()
    {
        LinuxScanner.ResetCache();

        var callCount = 0;
        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1)
                {
                    throw new InvalidOperationException("Simulated Docker failure");
                }

                return (SyftOutputNoAuthorOrLicense, string.Empty);
            });

        var enabledTypes = new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Npm,
            ComponentType.Pip,
        };

        var layers = new[]
        {
            new DockerLayer
            {
                LayerIndex = 0,
                DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
            },
        };

        // First call should fail.
        Func<Task> firstCall = async () =>
            await this.linuxScanner.ScanLinuxAsync(
                "retry_hash",
                layers,
                0,
                enabledTypes,
                LinuxScannerScope.AllLayers
            );

        await firstCall.Should().ThrowAsync<InvalidOperationException>();

        // Second call should succeed because the failed cache entry was removed.
        var result = await this.linuxScanner.ScanLinuxAsync(
            "retry_hash",
            layers,
            0,
            enabledTypes,
            LinuxScannerScope.AllLayers
        );

        result.Should().NotBeEmpty();
        result.First().Components.Should().ContainSingle();

        this.mockDockerService.Verify(
            service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [TestMethod]
    public async Task TestLinuxScanner_CancelledCaller_DoesNotBlockOnInFlightSyftRunAsync()
    {
        LinuxScanner.ResetCache();

        // Use a TCS to control when the syft container "completes",
        // so the first caller's run stays in-flight while we cancel the second.
        var syftCompletionSource = new TaskCompletionSource<(string, string)>();

        this.mockDockerService.Setup(service =>
                service.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(syftCompletionSource.Task);

        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };

        var layers = new[]
        {
            new DockerLayer
            {
                LayerIndex = 0,
                DiffId = "sha256:f95fc50d21d981f1efe1f04109c2c3287c271794f5d9e4fdf9888851a174a971",
            },
        };

        var scanner1 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );
        var scanner2 = new LinuxScanner(
            this.mockDockerService.Object,
            this.mockLogger.Object,
            this.componentFactories,
            this.artifactFilters
        );

        // First caller starts the syft run (it will block on syftCompletionSource).
        var task1 = scanner1.ScanLinuxAsync("cancel_hash", layers, 0, enabledTypes, LinuxScannerScope.AllLayers);

        // Second caller with a cancellable token joins the same in-flight run.
        using var cts = new CancellationTokenSource();
        var task2 = scanner2.ScanLinuxAsync("cancel_hash", layers, 0, enabledTypes, LinuxScannerScope.AllLayers, cts.Token);

        // Cancel the second caller while the first is still running.
        await cts.CancelAsync();

        // The second caller should throw OperationCanceledException promptly.
        try
        {
            await task2;
            Assert.Fail("Expected OperationCanceledException was not thrown");
        }
        catch (OperationCanceledException)
        {
            // Expected — the second caller was cancelled while waiting for the in-flight run.
        }

        // The first caller should still be running (not cancelled).
        task1.IsCompleted.Should().BeFalse();

        // Now let the first caller complete normally.
        syftCompletionSource.SetResult((SyftOutputNoAuthorOrLicense, string.Empty));
        var result1 = await task1;
        result1.Should().NotBeEmpty();
    }

    [TestMethod]
    public void TestLinuxScanner_ProcessSyftOutput_ExcludesPackageManagerDatabasePathsFromLayerAttribution()
    {
        // Simulates a scenario where a package (curl) is installed in layer1,
        // but the dpkg status file is also modified in layer2 by a different package install.
        // curl should only be attributed to layer1.
        var syftOutputJson = """
            {
                "distro": { "id": "ubuntu", "versionID": "22.04" },
                "artifacts": [
                    {
                        "name": "curl",
                        "version": "7.81.0",
                        "type": "deb",
                        "locations": [
                            {
                                "path": "/usr/bin/curl",
                                "layerID": "sha256:layer1"
                            },
                            {
                                "path": "/var/lib/dpkg/status",
                                "layerID": "sha256:layer2"
                            }
                        ]
                    }
                ],
                "source": {
                    "id": "sha256:abc",
                    "name": "test-image",
                    "type": "image",
                    "version": "sha256:abc"
                }
            }
            """;
        var syftOutput = SyftOutput.FromJson(syftOutputJson);
        var containerLayers = new List<DockerLayer>
        {
            new() { DiffId = "sha256:layer1", LayerIndex = 0, IsBaseImage = true },
            new() { DiffId = "sha256:layer2", LayerIndex = 1, IsBaseImage = false },
        };
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };

        var result = this.linuxScanner.ProcessSyftOutput(syftOutput, containerLayers, enabledTypes).ToList();

        // curl should only appear in layer1, not layer2
        var layer1Entry = result.FirstOrDefault(r => r.DockerLayer.DiffId == "sha256:layer1");
        var layer2Entry = result.FirstOrDefault(r => r.DockerLayer.DiffId == "sha256:layer2");

        layer1Entry.Should().NotBeNull();
        layer1Entry.Components.Should().ContainSingle();
        ((LinuxComponent)layer1Entry.Components.First()).Name.Should().Be("curl");

        // layer2 should have no components (or not exist in results)
        layer2Entry?.Components.Should().BeEmpty();
    }

    [TestMethod]
    public void TestLinuxScanner_ProcessSyftOutput_PackageWithOnlyDatabasePath_FallsBackToDatabaseLayer()
    {
        // If a package only has the database path as its location (no real file paths
        // and no metadata files), the database path layer is used as a fallback.
        var syftOutputJson = """
            {
                "distro": { "id": "alpine", "versionID": "3.18" },
                "artifacts": [
                    {
                        "name": "musl",
                        "version": "1.2.4",
                        "type": "apk",
                        "locations": [
                            {
                                "path": "/lib/apk/db/installed",
                                "layerID": "sha256:layer1"
                            }
                        ]
                    }
                ],
                "source": {
                    "id": "sha256:abc",
                    "name": "test-image",
                    "type": "image",
                    "version": "sha256:abc"
                }
            }
            """;
        var syftOutput = SyftOutput.FromJson(syftOutputJson);
        var containerLayers = new List<DockerLayer>
        {
            new() { DiffId = "sha256:layer1", LayerIndex = 0, IsBaseImage = true },
        };
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };

        var result = this.linuxScanner.ProcessSyftOutput(syftOutput, containerLayers, enabledTypes).ToList();

        // The component should fall back to the database path's layer
        var layer1Entry = result.FirstOrDefault(r => r.DockerLayer.DiffId == "sha256:layer1");
        layer1Entry.Should().NotBeNull();
        layer1Entry.Components.Should().ContainSingle();
        ((LinuxComponent)layer1Entry.Components.First()).Name.Should().Be("musl");
    }

    [TestMethod]
    public void TestLinuxScanner_ProcessSyftOutput_UsesMetadataFilesForLayerAttribution()
    {
        // Simulates a scenario where a package (curl) only has the package DB in its
        // artifact locations, but has owned files in metadata.files. The top-level files[]
        // listing provides the layer mapping for those owned files. The component should
        // be attributed to the layer of its owned files, not the DB layer.
        var syftOutputJson = """
            {
                "distro": { "id": "mariner", "versionID": "3.0" },
                "artifacts": [
                    {
                        "name": "curl",
                        "version": "8.11.1",
                        "type": "rpm",
                        "locations": [
                            {
                                "path": "/var/lib/rpm/rpmdb.sqlite",
                                "layerID": "sha256:layer2"
                            }
                        ],
                        "metadata": {
                            "files": [
                                { "path": "/usr/bin/curl" },
                                { "path": "/usr/lib/libcurl.so" }
                            ]
                        }
                    }
                ],
                "files": [
                    {
                        "id": "file1",
                        "location": {
                            "path": "/usr/bin/curl",
                            "layerID": "sha256:layer1"
                        }
                    },
                    {
                        "id": "file2",
                        "location": {
                            "path": "/usr/lib/libcurl.so",
                            "layerID": "sha256:layer1"
                        }
                    },
                    {
                        "id": "file3",
                        "location": {
                            "path": "/var/lib/rpm/rpmdb.sqlite",
                            "layerID": "sha256:layer2"
                        }
                    }
                ],
                "source": {
                    "id": "sha256:abc",
                    "name": "test-image",
                    "type": "image",
                    "version": "sha256:abc"
                }
            }
            """;
        var syftOutput = SyftOutput.FromJson(syftOutputJson);
        var containerLayers = new List<DockerLayer>
        {
            new() { DiffId = "sha256:layer1", LayerIndex = 0, IsBaseImage = true },
            new() { DiffId = "sha256:layer2", LayerIndex = 1, IsBaseImage = false },
        };
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };

        var result = this.linuxScanner.ProcessSyftOutput(syftOutput, containerLayers, enabledTypes).ToList();

        // curl should be attributed to layer1 (where its real files are), not layer2 (DB layer)
        var layer1Entry = result.FirstOrDefault(r => r.DockerLayer.DiffId == "sha256:layer1");
        var layer2Entry = result.FirstOrDefault(r => r.DockerLayer.DiffId == "sha256:layer2");

        layer1Entry.Should().NotBeNull();
        layer1Entry.Components.Should().ContainSingle();
        ((LinuxComponent)layer1Entry.Components.First()).Name.Should().Be("curl");

        layer2Entry?.Components.Should().BeEmpty();
    }
}
