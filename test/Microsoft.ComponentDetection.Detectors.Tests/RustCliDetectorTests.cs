#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustCliDetectorTests : BaseDetectorTest<RustCliDetector>
{
    private readonly string mockMetadataV1 = @"
{
    ""packages"": [
        {
            ""name"": ""registry-package-1"",
            ""version"": ""1.0.1"",
            ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
            ""license"": null,
            ""license_file"": null,
            ""description"": ""test registry package 1"",
            ""source"": ""registry+https://test.com/registry-package-1"",
            ""dependencies"": [
                {
                    ""name"": ""inner-dependency-1"",
                    ""source"": ""registry+registry+https://test.com/inner-dependency-1"",
                    ""req"": ""^0.3.0"",
                    ""kind"": ""dev"",
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                }
            ]
        },
        {
            ""name"": ""rust-test"",
            ""version"": ""0.1.0"",
            ""id"": ""rust-test 0.1.0 (path+file:///C:/test)"",
            ""license"": null,
            ""license_file"": null,
            ""description"": null,
            ""source"": null,
            ""dependencies"": [
                {
                    ""name"": ""registry-package-1"",
                    ""source"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""req"": ""^1.0.1"",
                    ""kind"": null,
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                },
                {
                    ""name"": ""rust-test-inner"",
                    ""source"": ""(path+file:///C:/test/rust-test-inner)"",
                    ""req"": ""*"",
                    ""kind"": null,
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null,
                    ""path"": ""C:\\test\\rust-test-inner""
                },
                {
                    ""name"": ""dev-dependency-1"",
                    ""source"": ""registry+https://test.com/dev-dependency-1"",
                    ""req"": ""^0.4.0"",
                    ""kind"": ""dev"",
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                }
            ]
        },
        {
            ""name"": ""rust-test-inner"",
            ""version"": ""0.1.0"",
            ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
            ""license"": null,
            ""license_file"": null,
            ""description"": null,
            ""source"": null,
            ""dependencies"": []
        },
        {
            ""name"": ""dev-dependency-1"",
            ""version"": ""0.4.0"",
            ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
            ""license"": null,
            ""license_file"": null,
            ""description"": ""test dev dependency"",
            ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
            ""dependencies"": []
        }
    ],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""workspace_default_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///C:/test)"",
                ""dependencies"": [
                    ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                    ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)""
                ],
                ""deps"": [
                    {
                        ""name"": ""registry-package-1"",
                        ""pkg"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    },
                    {
                        ""name"": ""cargo"",
                        ""pkg"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    },
                    {
                        ""name"": ""dev-dependency-1"",
                        ""pkg"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": ""dev"",
                                ""target"": null
                            }
                        ]
                    }
                ],
                ""features"": []
            },
            {
                ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": []
            },
            {
                ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": []
            }
        ],
        ""root"": ""rust-test 0.1.0 (path+file:///C:/test)""
    },
    ""target_directory"": ""C:\\test"",
    ""version"": 1,
    ""workspace_root"": ""C:\\test"",
    ""metadata"": null
}";

    private readonly string mockMetadataWithLicenses = @"
{
    ""packages"": [
        {
            ""name"": ""registry-package-1"",
            ""version"": ""1.0.1"",
            ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": ""test registry package 1"",
            ""source"": ""registry+https://test.com/registry-package-1"",
            ""dependencies"": [
                {
                    ""name"": ""inner-dependency-1"",
                    ""source"": ""registry+registry+https://test.com/inner-dependency-1"",
                    ""req"": ""^0.3.0"",
                    ""kind"": ""dev"",
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                }
            ]
        },
        {
            ""name"": ""rust-test"",
            ""version"": ""0.1.0"",
            ""id"": ""rust-test 0.1.0 (path+file:///C:/test)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": null,
            ""source"": null,
            ""dependencies"": [
                {
                    ""name"": ""registry-package-1"",
                    ""source"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""req"": ""^1.0.1"",
                    ""kind"": null,
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                },
                {
                    ""name"": ""rust-test-inner"",
                    ""source"": ""(path+file:///C:/test/rust-test-inner)"",
                    ""req"": ""*"",
                    ""kind"": null,
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null,
                    ""path"": ""C:\\test\\rust-test-inner""
                },
                {
                    ""name"": ""dev-dependency-1"",
                    ""source"": ""registry+https://test.com/dev-dependency-1"",
                    ""req"": ""^0.4.0"",
                    ""kind"": ""dev"",
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                }
            ]
        },
        {
            ""name"": ""rust-test-inner"",
            ""version"": ""0.1.0"",
            ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": null,
            ""source"": null,
            ""dependencies"": []
        },
        {
            ""name"": ""dev-dependency-1"",
            ""version"": ""0.4.0"",
            ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": ""test dev dependency"",
            ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
            ""dependencies"": []
        }
    ],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""workspace_default_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///C:/test)"",
                ""dependencies"": [
                    ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                    ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)""
                ],
                ""deps"": [
                    {
                        ""name"": ""registry-package-1"",
                        ""pkg"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    },
                    {
                        ""name"": ""cargo"",
                        ""pkg"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    },
                    {
                        ""name"": ""dev-dependency-1"",
                        ""pkg"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                        ""dep_kinds"": [
                            {
                                ""kind"": ""dev"",
                                ""target"": null
                            }
                        ]
                    }
                ],
                ""features"": []
            },
            {
                ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": []
            },
            {
                ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": []
            }
        ],
        ""root"": ""rust-test 0.1.0 (path+file:///C:/test)""
    },
    ""target_directory"": ""C:\\test"",
    ""version"": 1,
    ""workspace_root"": ""C:\\test"",
    ""metadata"": null
}";

    private readonly string mockMetadataVirtualManifest = @"
{
    ""packages"": [
        {
            ""name"": ""registry-package-1"",
            ""version"": ""1.0.1"",
            ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": ""test registry package 1"",
            ""source"": ""registry+https://test.com/registry-package-1"",
            ""dependencies"": [
                {
                    ""name"": ""inner-dependency-1"",
                    ""source"": ""registry+registry+https://test.com/inner-dependency-1"",
                    ""req"": ""^0.3.0"",
                    ""kind"": ""dev"",
                    ""rename"": null,
                    ""optional"": false,
                    ""uses_default_features"": true,
                    ""features"": [],
                    ""target"": null,
                    ""registry"": null
                }
            ]
        },
        {
            ""name"": ""rust-test-inner"",
            ""version"": ""0.1.0"",
            ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": null,
            ""source"": null,
            ""dependencies"": []
        },
        {
            ""name"": ""dev-dependency-1"",
            ""version"": ""0.4.0"",
            ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
            ""license"": ""MIT"",
            ""authors"": [
                ""Sample Author 1"",
                ""Sample Author 2""
            ],
            ""license_file"": null,
            ""description"": ""test dev dependency"",
            ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
            ""dependencies"": []
        }
    ],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""workspace_default_members"": [
        ""rust-test 0.1.0 (path+file:///C:/test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test-inner 0.1.0 (path+file:///C:/test/rust-test-inner)"",
                ""dependencies"": [
                    ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)""
                ],
                ""deps"": [
                {
                    ""name"": ""registry-package-1"",
                    ""pkg"": ""registry-package-1 1.0.1 (registry+https://test.com/registry-package-1)"",
                    ""dep_kinds"": [
                        {
                            ""kind"": null,
                            ""target"": null
                        }
                    ]
                },
                {
                    ""name"": ""dev-dependency-1"",
                    ""pkg"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                    ""dep_kinds"": [
                        {
                            ""kind"": ""dev"",
                            ""target"": null
                        }
                    ]
                }],
                ""features"": []
            },
            {
                ""id"": ""dev-dependency-1 0.4.0 (registry+https://test.com/dev-dependency-1)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": []
            }
        ],
        ""root"": null
    },
    ""target_directory"": ""C:\\test"",
    ""version"": 1,
    ""workspace_root"": ""C:\\test"",
    ""metadata"": null
}";

    private Mock<ICommandLineInvocationService> mockCliService;

    private Mock<IEnvironmentVariableService> mockEnvVarService;

    private Mock<IComponentStreamEnumerableFactory> mockComponentStreamEnumerableFactory;

    [TestInitialize]
    public void InitMocks()
    {
        this.mockCliService = new Mock<ICommandLineInvocationService>();
        this.DetectorTestUtility.AddServiceMock(this.mockCliService);
        this.mockComponentStreamEnumerableFactory = new Mock<IComponentStreamEnumerableFactory>();
        this.DetectorTestUtility.AddServiceMock(this.mockComponentStreamEnumerableFactory);
        this.mockEnvVarService = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.mockEnvVarService);
    }

    [TestMethod]
    public async Task RustCLiDetector_CommandCantBeLocatedSuccessAsync()
    {
        this.mockCliService
            .Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(false);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_FailExecutingCommandSuccessAsync()
    {
        this.mockCliService
            .Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(true);
        this.mockCliService
            .Setup(x => x.ExecuteCommandAsync("cargo", It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ThrowsAsync(new InvalidOperationException());

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_RespectsFallBackVariableAsync()
    {
        var testCargoLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]";
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .Throws(new InvalidOperationException());
        this.mockEnvVarService
                    .Setup(x => x.IsEnvironmentVariableValueTrue("DisableRustCliScan"))
                    .Returns(true);
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(testCargoLockString);
        await writer.FlushAsync();
        stream.Position = 0;
        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns([new ComponentStream() { Location = "Cargo.toml", Stream = stream }]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("other_dependency_dependency 0.1.12-alpha.6 - Cargo", "my_dev_dependency 1.0.0 - Cargo", "my_dependency 1.0.0 - Cargo", "other_dependency 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be(null);
                cargoComponent.License.Should().Be(null);
            }
        }

        return;
    }

    [TestMethod]
    public async Task RustCliDetector_HandlesNonZeroExitCodeAsync()
    {
        var cargoMetadata = this.mockMetadataV1;
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata, ExitCode = -1 });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_RegistersCorrectRootDepsAsync()
    {
        var cargoMetadata = this.mockMetadataV1;
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("registry-package-1 1.0.1 - Cargo", "dev-dependency-1 0.4.0 - Cargo");
    }

    [TestMethod]
    public async Task RustCliDetector_NotInGraphAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""path+file:///home/justin/rust-test#rust-test@0.1.0"",
                ""dependencies"": [
                    ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    }
                ],
                ""features"": []
            }
        ],
        ""root"": ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    },
    ""target_directory"": ""/home/justin/rust-test/target"",
    ""version"": 1,
    ""workspace_root"": ""/home/justin/rust-test"",
    ""metadata"": null
}";
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_InvalidNameAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""path+file:///home/justin/rust-test#rust-test@0.1.0"",
                ""dependencies"": [
                    ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""INVALID"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    }
                ],
                ""features"": []
            }
        ],
        ""root"": ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    },
    ""target_directory"": ""/home/justin/rust-test/target"",
    ""version"": 1,
    ""workspace_root"": ""/home/justin/rust-test"",
    ""metadata"": null
}";
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_ComponentContainsAuthorAndLicenseAsync()
    {
        var cargoMetadata = this.mockMetadataWithLicenses;
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("registry-package-1 1.0.1 - Cargo", "dev-dependency-1 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be("Sample Author 1, Sample Author 2");
                cargoComponent.License.Should().Be("MIT");
            }
        }
    }

    [TestMethod]
    public async Task RustCliDetector_AuthorAndLicenseNullAsync()
    {
        var cargoMetadata = this.mockMetadataV1;
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("registry-package-1 1.0.1 - Cargo", "dev-dependency-1 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be(null);
                cargoComponent.License.Should().Be(null);
            }
        }
    }

    [TestMethod]
    public async Task RustCliDetector_AuthorAndLicenseEmptyStringAsync()
    {
        var cargoMetadata = @"
{
    ""workspace_members"": [
        ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""path+file:///home/justin/rust-test#rust-test@0.1.0"",
                ""dependencies"": [
                    ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147"",
                        ""dep_kinds"": [
                            {
                                ""kind"": null,
                                ""target"": null
                            }
                        ]
                    }
                ],
                ""features"": []
            }
        ],
        ""root"": ""path+file:///home/justin/rust-test#rust-test@0.1.0""
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#libc@0.2.147"",
      ""license"": """",
      ""license_file"": null,
      ""description"": """",
      ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
      ""dependencies"": [],
      ""targets"": [],
      ""features"": {},
      ""manifest_path"": """",
      ""metadata"": {},
      ""publish"": null,
      ""authors"": [
        """"
      ],
      ""categories"": [],
      ""keywords"": [],
      ""readme"": ""README.md"",
      ""repository"": ""https://github.com/tkaitchuck/ahash"",
      ""homepage"": null,
      ""documentation"": """",
      ""edition"": ""00"",
      ""links"": null,
      ""default_run"": null,
      ""rust_version"": null
    },
    {
      ""name"": ""rust-test"",
      ""version"": ""0.1.0"",
      ""id"": ""path+file:///home/justin/rust-test#rust-test@0.1.0"",
      ""license"": """",
      ""license_file"": null,
      ""description"": ""A non-cryptographic hash function using AES-NI for high performance"",
      ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
      ""dependencies"": [],
      ""targets"": [],
      ""features"": {},
      ""manifest_path"": """",
      ""metadata"": {},
      ""publish"": null,
      ""authors"": [
        """"
      ],
      ""categories"": [],
      ""keywords"": [],
      ""readme"": ""README.md"",
      ""repository"": ""https://github.com/tkaitchuck/ahash"",
      ""homepage"": null,
      ""documentation"": """",
      ""edition"": ""000"",
      ""links"": null,
      ""default_run"": null,
      ""rust_version"": null
    }
],
    ""target_directory"": ""/home/justin/rust-test/target"",
    ""version"": 1,
    ""workspace_root"": ""/home/justin/rust-test"",
    ""metadata"": null
}";

        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be(null);
                cargoComponent.License.Should().Be(null);
            }
        }
    }

    [TestMethod]
    public async Task RustCliDetector_VirtualManifestSuccessfullyProcessedAsync()
    {
        var cargoMetadata = this.mockMetadataVirtualManifest;

        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("registry-package-1 1.0.1 - Cargo", "dev-dependency-1 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();
        return;
    }

    [TestMethod]
    public async Task RustCliDetector_FallBackLogicFailsIfNoCargoLockFoundAsync()
    {
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = string.Empty, ExitCode = -1 });

        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns(Enumerable.Empty<ComponentStream>());

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RustCliDetector_FallBackLogicTriggeredOnFailedCargoCommandAsync()
    {
        var testCargoLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]";
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = null, ExitCode = -1 });

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(testCargoLockString);
        await writer.FlushAsync();
        stream.Position = 0;
        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns([new ComponentStream() { Location = "Cargo.toml", Stream = stream }]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("other_dependency_dependency 0.1.12-alpha.6 - Cargo", "my_dev_dependency 1.0.0 - Cargo", "my_dependency 1.0.0 - Cargo", "other_dependency 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be(null);
                cargoComponent.License.Should().Be(null);
            }
        }

        return;
    }

    [TestMethod]
    public async Task RustCliDetector_FallBackLogicTriggeredOnFailedProcessingAsync()
    {
        var testCargoLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]";
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .Throws(new InvalidOperationException());

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(testCargoLockString);
        await writer.FlushAsync();
        stream.Position = 0;
        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns([new ComponentStream() { Location = "Cargo.toml", Stream = stream }]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("other_dependency_dependency 0.1.12-alpha.6 - Cargo", "my_dev_dependency 1.0.0 - Cargo", "my_dependency 1.0.0 - Cargo", "other_dependency 0.4.0 - Cargo");

        var components = componentRecorder.GetDetectedComponents();

        foreach (var component in components)
        {
            if (component.Component is CargoComponent cargoComponent)
            {
                cargoComponent.Author.Should().Be(null);
                cargoComponent.License.Should().Be(null);
            }
        }

        return;
    }

    [TestMethod]
    public async Task RustCliDetector_FallBackLogicSkippedOnWorkspaceErrorAsync()
    {
        this.mockCliService.Setup(x => x.CanCommandBeLocatedAsync("cargo", It.IsAny<IEnumerable<string>>())).ReturnsAsync(true);
        this.mockCliService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = null, StdErr = "current package believes it's in a workspace when it's not:", ExitCode = -1 });
        var testCargoLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]";
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(testCargoLockString);
        await writer.FlushAsync();
        stream.Position = 0;
        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns([new ComponentStream() { Location = "Cargo.toml", Stream = stream }]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();

        return;
    }
}
