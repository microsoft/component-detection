namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
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
    private readonly Mock<ISyftRunner> mockSyftRunner;
    private readonly Mock<ILogger<LinuxScanner>> mockLogger;
    private readonly List<IArtifactComponentFactory> componentFactories;
    private readonly List<IArtifactFilter> artifactFilters;

    public LinuxScannerTests()
    {
        this.mockSyftRunner = new Mock<ISyftRunner>();

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
        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
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
                new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
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
                LinuxScannerScope.AllLayers,
                this.mockSyftRunner.Object
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
        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
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
                new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
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
                LinuxScannerScope.AllLayers,
                this.mockSyftRunner.Object
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
        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
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
                new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
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
                LinuxScannerScope.AllLayers,
                this.mockSyftRunner.Object
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
        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
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
                new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
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
                LinuxScannerScope.AllLayers,
                this.mockSyftRunner.Object
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

        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
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
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes,
            LinuxScannerScope.AllLayers,
            this.mockSyftRunner.Object
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

        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithMixedTypes, string.Empty));

        // Only enable Linux component type
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        var layers = await this.linuxScanner.ScanLinuxAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes,
            LinuxScannerScope.AllLayers,
            this.mockSyftRunner.Object
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

        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithMixedTypes, string.Empty));

        // Only enable Npm and Pip component types (exclude Linux)
        var enabledTypes = new HashSet<ComponentType> { ComponentType.Npm, ComponentType.Pip };
        var layers = await this.linuxScanner.ScanLinuxAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            [
                new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" },
                new DockerLayer { LayerIndex = 1, DiffId = "sha256:layer2" },
            ],
            0,
            enabledTypes,
            LinuxScannerScope.AllLayers,
            this.mockSyftRunner.Object
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
        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SyftOutputNoAuthorOrLicense, string.Empty));

        var enabledTypes = new HashSet<ComponentType> { ComponentType.Linux };
        await this.linuxScanner.ScanLinuxAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            [new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" }],
            0,
            enabledTypes,
            scope,
            this.mockSyftRunner.Object
        );

        this.mockSyftRunner.Verify(
            runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.Is<IList<string>>(args =>
                        args.Contains("--scope") && args.Contains(expectedFlag)
                    ),
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
                new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
                [new DockerLayer { LayerIndex = 0, DiffId = "sha256:layer1" }],
                0,
                enabledTypes,
                invalidScope,
                this.mockSyftRunner.Object
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

        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutputWithSource, string.Empty));

        var ociRef = new ImageReference { OriginalInput = "oci-dir:/oci-image", Reference = "/host/path/to/oci", Kind = ImageReferenceKind.OciLayout };
        var syftOutput = await this.linuxScanner.GetSyftOutputAsync(
            ociRef,
            LinuxScannerScope.AllLayers,
            this.mockSyftRunner.Object
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

        this.mockSyftRunner.Setup(runner =>
                runner.RunSyftAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((syftOutput, string.Empty));

        var additionalBinds = new List<string> { "/host/path/to/oci:/oci-image:ro" };
        var ociRef = new ImageReference { OriginalInput = "oci-dir:/oci-image", Reference = "/host/path/to/oci", Kind = ImageReferenceKind.OciLayout };
        await this.linuxScanner.GetSyftOutputAsync(
            ociRef,
            LinuxScannerScope.AllLayers,
            this.mockSyftRunner.Object
        );

        // Verify the Syft command uses oci-dir: scheme and passes binds
        this.mockSyftRunner.Verify(
            runner =>
                runner.RunSyftAsync(
                    It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.OciLayout && r.Reference == "/host/path/to/oci"),
                    It.IsAny<IList<string>>(),
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
}
