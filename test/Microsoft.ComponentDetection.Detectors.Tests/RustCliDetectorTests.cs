namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
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
}
