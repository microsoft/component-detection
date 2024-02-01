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
    private Mock<ICommandLineInvocationService> mockCliService;
    private Mock<IComponentStreamEnumerableFactory> mockComponentStreamEnumerableFactory;

    [TestInitialize]
    public void InitCliMock()
    {
        this.mockCliService = new Mock<ICommandLineInvocationService>();
        this.DetectorTestUtility.AddServiceMock(this.mockCliService);
        this.mockComponentStreamEnumerableFactory = new Mock<IComponentStreamEnumerableFactory>();
        this.DetectorTestUtility.AddServiceMock(this.mockComponentStreamEnumerableFactory);
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
    public async Task RustCliDetector_HandlesNonZeroExitCodeAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    },
    ""target_directory"": ""/home/justin/rust-test/target"",
    ""version"": 1,
    ""workspace_root"": ""/home/justin/rust-test"",
    ""metadata"": null
}";
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
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo", "rust-test 0.1.0 - Cargo");
    }

    [TestMethod]
    public async Task RustCliDetector_NotInGraphAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);
        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo", "rust-test 0.1.0 - Cargo");
    }

    [TestMethod]
    public async Task RustCliDetector_InvalidNameAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(1);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("rust-test 0.1.0 - Cargo");
    }

    [TestMethod]
    public async Task RustCliDetector_ComponentContainsAuthorAndLicenseAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
      ""license"": ""MIT"",
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
        ""Sample Author 1"",
        ""Sample Author 2""
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
      ""id"": ""rust-test (registry+https://github.com/rust-lang/crates.io-index)"",
      ""license"": ""MIT"",
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
        ""Sample Author 1"",
        ""Sample Author 2""
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo", "rust-test 0.1.0 - Cargo");

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
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
      ""license_file"": null,
      ""description"": """",
      ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
      ""dependencies"": [],
      ""targets"": [],
      ""features"": {},
      ""manifest_path"": """",
      ""metadata"": {},
      ""publish"": null,
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
      ""id"": ""rust-test (registry+https://github.com/rust-lang/crates.io-index)"",
      ""license_file"": null,
      ""description"": """",
      ""source"": ""registry+https://github.com/rust-lang/crates.io-index"",
      ""dependencies"": [],
      ""targets"": [],
      ""features"": {},
      ""manifest_path"": """",
      ""metadata"": {},
      ""publish"": null,
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo", "rust-test 0.1.0 - Cargo");

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
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
      ""id"": ""rust-test (registry+https://github.com/rust-lang/crates.io-index)"",
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        componentRecorder
            .GetDetectedComponents()
            .Select(x => x.Component.Id)
            .Should()
            .BeEquivalentTo("libc 0.2.147 - Cargo", "rust-test 0.1.0 - Cargo");

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
    public async Task RustCliDetector_FallBackLogicTriggeredOnVirtualManifestAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": null
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
      ""id"": ""rust-test (registry+https://github.com/rust-lang/crates.io-index)"",
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
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = cargoMetadata });

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(testCargoLockString);
        await writer.FlushAsync();
        stream.Position = 0;
        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns(new[] { new ComponentStream() { Location = "Cargo.toml", Stream = stream } });

        testCargoLockString = testCargoLockString.Replace("\r", string.Empty);

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
    public async Task RustCliDetector_FallBackLogicFailsIfNoCargoLockFoundAsync()
    {
        var cargoMetadata = @"
{
    ""packages"": [],
    ""workspace_members"": [
        ""rust-test 0.1.0 (path+file:///home/justin/rust-test)""
    ],
    ""resolve"": {
        ""nodes"": [
            {
                ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
                ""dependencies"": [],
                ""deps"": [],
                ""features"": [
                    ""default"",
                    ""std""
                ]
            },
            {
                ""id"": ""rust-test 0.1.0 (path+file:///home/justin/rust-test)"",
                ""dependencies"": [
                    ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)""
                ],
                ""deps"": [
                    {
                        ""name"": ""libc"",
                        ""pkg"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
        ""root"": null
    },
    ""packages"": [
    {
      ""name"": ""libc"",
      ""version"": ""0.2.147"",
      ""id"": ""libc 0.2.147 (registry+https://github.com/rust-lang/crates.io-index)"",
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
      ""id"": ""rust-test (registry+https://github.com/rust-lang/crates.io-index)"",
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

        this.mockComponentStreamEnumerableFactory.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), new List<string> { "Cargo.lock" }, It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns(Enumerable.Empty<ComponentStream>());

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(0);
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
            .Returns(new[] { new ComponentStream() { Location = "Cargo.toml", Stream = stream } });

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
            .Returns(new[] { new ComponentStream() { Location = "Cargo.toml", Stream = stream } });

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
}
