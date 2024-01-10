namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
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

    [TestInitialize]
    public void InitCliMock()
    {
        this.mockCliService = new Mock<ICommandLineInvocationService>();
        this.DetectorTestUtility.AddServiceMock(this.mockCliService);
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
        ""Sample Author""
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
}
