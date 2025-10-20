namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustSbomDetectorTests : BaseDetectorTest<RustSbomDetector>
{
    private readonly Mock<IRustMetadataContextBuilder> mockMetadataContextBuilder;
    private readonly Mock<IRustCliParser> mockCliParser;
    private readonly Mock<IRustCargoLockParser> mockCargoLockParser;
    private readonly Mock<IRustSbomParser> mockSbomParser;
    private readonly Mock<ILogger<RustSbomDetector>> mockLogger;

    private readonly string testSbom = /*lang=json,strict*/ @"
{
  ""version"": 1,
  ""root"": 0,
  ""crates"": [
    {
      ""id"": ""path+file:///temp/test-crate#0.1.0"",
      ""features"": [],
      ""dependencies"": [
        {
          ""index"": 1,
          ""kind"": ""normal""
        },
        {
          ""index"": 1,
          ""kind"": ""build""
        },
        {
          ""index"": 2,
          ""kind"": ""normal""
        }
      ]
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#my_dependency@1.0.0"",
      ""features"": [],
      ""unexpected_new_thing_from_the_future"": ""foo"",
      ""dependencies"": []
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#other_dependency@0.4.0"",
      ""features"": [],
      ""dependencies"": [
            {
              ""index"": 3,
              ""kind"": ""normal""
            }
        ]
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#other_dependency_dependency@0.1.12-alpha.6"",
      ""features"": [],
      ""dependencies"": []
    }
  ],
  ""rustc"": {
    ""version"": ""1.84.1"",
    ""wrapper"": null,
    ""workspace_wrapper"": null,
    ""commit_hash"": ""2b00e2aae6389eb20dbb690bce5a28cc50affa53"",
    ""host"": ""x86_64-pc-windows-msvc"",
    ""verbose_version"": ""rustc 1.84.1""
  },
  ""target"": ""x86_64-pc-windows-msvc""
}
";

    private readonly string testSbomWithGitDeps = /*lang=json,strict*/ @"{
    ""version"": 1,
    ""root"": 2,
    ""crates"": [
        {
            ""id"": ""registry+https://github.com/rust-lang/crates.io-index#aho-corasick@1.1.3"",
            ""features"": [
                ""perf-literal"",
                ""std""
            ],
            ""dependencies"": [
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""path+file:///D:/temp/hello#0.1.0"",
            ""features"": [],
            ""dependencies"": [
                {
                    ""index"": 4,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""path+file:///D:/temp/hello#0.1.0"",
            ""features"": [],
            ""dependencies"": [
                {
                    ""index"": 1,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 4,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""bin""
            ]
        },
        {
            ""id"": ""registry+https://github.com/rust-lang/crates.io-index#memchr@2.7.4"",
            ""features"": [
                ""alloc"",
                ""std""
            ],
            ""dependencies"": [],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex@1.11.1"",
            ""features"": [
            ],
            ""dependencies"": [
                {
                    ""index"": 0,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 5,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 6,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex-automata@0.4.9"",
            ""features"": [
            ],
            ""dependencies"": [
                {
                    ""index"": 0,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 6,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex-syntax@0.8.5"",
            ""features"": [
            ],
            ""dependencies"": [],
            ""kind"": [
                ""lib""
            ]
        }
    ],
    ""rustc"": {
        ""version"": ""1.88.0-nightly"",
        ""wrapper"": null,
        ""workspace_wrapper"": null,
        ""commit_hash"": ""6bc57c6bf7d0024ad9ea5a2c112f3fc9c383c8a4"",
        ""host"": ""x86_64-pc-windows-msvc"",
        ""verbose_version"": ""rustc 1.88.0-nightly (6bc57c6bf 2025-04-22)\nbinary: rustc\ncommit-hash: 6bc57c6bf7d0024ad9ea5a2c112f3fc9c383c8a4\ncommit-date: 2025-04-22\nhost: x86_64-pc-windows-msvc\nrelease: 1.88.0-nightly\nLLVM version: 20.1.2\n""
    },
    ""target"": ""x86_64-pc-windows-msvc""
}";

    private IPathUtilityService pathUtilityService;

    public RustSbomDetectorTests()
    {
        this.mockMetadataContextBuilder = new Mock<IRustMetadataContextBuilder>();
        this.mockCliParser = new Mock<IRustCliParser>();
        this.mockCargoLockParser = new Mock<IRustCargoLockParser>();
        this.mockSbomParser = new Mock<IRustSbomParser>();
        this.mockLogger = new Mock<ILogger<RustSbomDetector>>();
    }

    [TestInitialize]
    public void Initialize()
    {
        this.mockMetadataContextBuilder.Reset();
        this.mockCliParser.Reset();
        this.mockCargoLockParser.Reset();
        this.mockSbomParser.Reset();
        this.mockLogger.Reset();

        this.pathUtilityService = new PathUtilityService(new Mock<ILogger<PathUtilityService>>().Object);

        this.DetectorTestUtility.AddService(this.pathUtilityService);
        this.DetectorTestUtility.AddService(this.mockMetadataContextBuilder.Object);
        this.DetectorTestUtility.AddService(this.mockCliParser.Object);
        this.DetectorTestUtility.AddService(this.mockCargoLockParser.Object);
        this.DetectorTestUtility.AddService(this.mockSbomParser.Object);
    }

    [TestMethod]
    public async Task TestGraphIsCorrectAsync()
    {
        // Use real parser for this test
        this.DetectorTestUtility.AddService<IRustSbomParser>(new RustSbomParser(new Mock<ILogger<RustSbomParser>>().Object));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("main.exe.cargo-sbom.json", this.testSbom)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(3);

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var rootComponents = new List<string>
        {
            "my_dependency 1.0.0 - Cargo",
            "other_dependency 0.4.0 - Cargo",
        };

        rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

        graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEmpty();

        var other_dependencyDependencies = new List<string>
        {
            "other_dependency_dependency 0.1.12-alpha.6 - Cargo",
        };

        graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);
    }

    [TestMethod]
    public async Task TestGraphIsCorrectWithGitDeps()
    {
        // Use real parser for this test
        this.DetectorTestUtility.AddService<IRustSbomParser>(new RustSbomParser(new Mock<ILogger<RustSbomParser>>().Object));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("main.exe.cargo-sbom.json", this.testSbomWithGitDeps)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        graph.GetDependenciesForComponent("aho-corasick 1.1.3 - Cargo").Should().BeEquivalentTo("memchr 2.7.4 - Cargo");
    }

    [TestMethod]
    public void TestDetectorProperties()
    {
        var detector = new RustSbomDetector(
            null,
            null,
            this.mockLogger.Object,
            this.mockMetadataContextBuilder.Object,
            this.pathUtilityService,
            this.mockCliParser.Object,
            this.mockSbomParser.Object,
            this.mockCargoLockParser.Object,
            null);

        detector.Id.Should().Be("RustSbom");
        detector.Categories.Should().BeEquivalentTo(["Rust"]);
        detector.SupportedComponentTypes.Should().BeEquivalentTo([ComponentType.Cargo]);
        detector.Version.Should().Be(1);
        detector.SearchPatterns.Should().BeEquivalentTo(["Cargo.toml", "Cargo.lock", "*.cargo-sbom.json"]);
    }

    [TestMethod]
    public async Task TestSbomOnlyMode_WithOwnershipMap_VerifiesOwnershipUsed()
    {
        var sbomContent = /*lang=json,strict*/ @"
{
  ""version"": 1,
  ""root"": 0,
  ""crates"": []
}";

        var ownershipResult = new IRustMetadataContextBuilder.OwnershipResult
        {
            PackageToTomls = new Dictionary<string, HashSet<string>>
            {
                ["test"] = ["/path/Cargo.toml"],
            },
        };

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownershipResult);

        // Track whether ownership was passed to the parser
        IReadOnlyDictionary<string, HashSet<string>> capturedOwnership = null;
        this.mockSbomParser
            .Setup(x => x.ParseWithOwnershipAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, IComponentRecorder, IReadOnlyDictionary<string, HashSet<string>>, CancellationToken>(
                (stream, recorder, parentRecorder, ownership, token) =>
                {
                    capturedOwnership = ownership;
                })
            .ReturnsAsync(1);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("test.cargo-sbom.json", sbomContent)
            .WithFile("Cargo.toml", "[package]\nname = \"test\"\nversion = \"1.0.0\"")
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Verify metadata context was built
        this.mockMetadataContextBuilder.Verify(
            x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify ParseWithOwnershipAsync was called (not ParseAsync)
        this.mockSbomParser.Verify(
            x => x.ParseWithOwnershipAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify the ownership map was actually passed
        capturedOwnership.Should().NotBeNull();
        capturedOwnership.Should().ContainKey("test");
        capturedOwnership["test"].Should().Contain("/path/Cargo.toml");
    }

    [TestMethod]
    public async Task TestMultipleSbomFiles_ProcessedInAlphabeticalOrder_VerifiesOrder()
    {
        var sbom1 = Path.Combine(Path.GetTempPath(), "a.cargo-sbom.json");
        var sbom2 = Path.Combine(Path.GetTempPath(), "z.cargo-sbom.json");

        var sbomContent = "{\"version\":1,\"root\":0,\"crates\":[]}";

        var processedFiles = new List<string>();

        this.mockSbomParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, CancellationToken>(
                (stream, recorder, token) =>
                {
                    processedFiles.Add(stream.Location);
                })
            .ReturnsAsync(1);

        await this.DetectorTestUtility
            .WithFile("z.cargo-sbom.json", sbomContent, fileLocation: sbom2)
            .WithFile("a.cargo-sbom.json", sbomContent, fileLocation: sbom1)
            .ExecuteDetectorAsync();

        // Verify both files were processed
        processedFiles.Should().HaveCount(2);

        // Verify they were processed in alphabetical order
        processedFiles[0].Should().Be(sbom1);
        processedFiles[1].Should().Be(sbom2);
    }

    [TestMethod]
    public async Task TestWorkspaceGlobRules_MemberDirectoriesSkipped()
    {
        var workspaceDir = Path.Combine(Path.GetTempPath(), "workspace");
        var rootLock = Path.Combine(workspaceDir, "Cargo.lock");
        var rootToml = Path.Combine(workspaceDir, "Cargo.toml");
        var member1Lock = Path.Combine(workspaceDir, "member1", "Cargo.lock");

        var cargoLockContent = @"
[[package]]
name = ""test""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

        // Workspace-only TOML (no [package] section)
        var workspaceTomlContent = @"
[workspace]
members = [""member1"", ""member2""]
exclude = [""tests/*""]
";

        // Mock IFileUtilityService to simulate file system operations
        var mockFileUtilityService = new Mock<IFileUtilityService>();

        // Mock Exists to return true for the workspace Cargo.toml
        mockFileUtilityService
            .Setup(x => x.Exists(rootToml))
            .Returns(true);

        // Mock ReadAllText to return the workspace TOML content
        mockFileUtilityService
            .Setup(x => x.ReadAllText(rootToml))
            .Returns(workspaceTomlContent);

        var lockCallCount = 0;
        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, CancellationToken>(
                (stream, recorder, token) => lockCallCount++)
            .ReturnsAsync(2);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .AddService(mockFileUtilityService.Object)
            .WithFile("Cargo.lock", cargoLockContent, fileLocation: rootLock)
            .WithFile("Cargo.toml", workspaceTomlContent, fileLocation: rootToml)
            .WithFile("Cargo.lock", cargoLockContent, fileLocation: member1Lock)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Root Cargo.lock should be processed
        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.Is<IComponentStream>(s => s.Location == rootLock),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Member Cargo.lock should be skipped due to workspace glob rules
        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.Is<IComponentStream>(s => s.Location == member1Lock),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Only one lock file should have been processed
        lockCallCount.Should().Be(1, "Only the root workspace Cargo.lock should be processed");

        // Verify File.Exists was called to check for Cargo.toml
        mockFileUtilityService.Verify(
            x => x.Exists(rootToml),
            Times.Once,
            "Should check if Cargo.toml exists in same directory as Cargo.lock");

        // Verify ReadAllText was called to read the workspace TOML content
        mockFileUtilityService.Verify(
            x => x.ReadAllText(rootToml),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task TestFallbackMode_CargoTomlWithoutMetadataCache_LogsWarning()
    {
        var cargoTomlContent = "[package]\nname = \"test\"\nversion = \"1.0.0\"";
        var tomlPath = Path.Combine(Path.GetTempPath(), "missing", "Cargo.toml");

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IRustMetadataContextBuilder.OwnershipResult
            {
                ManifestToMetadata = [],
            });

        // Create a new mock logger to capture warnings
        var mockLoggerForDetector = new Mock<ILogger<RustSbomDetector>>();

        var (result, componentRecorder) = await this.DetectorTestUtility
            .AddServiceMock(mockLoggerForDetector)
            .WithFile("Cargo.toml", cargoTomlContent, fileLocation: tomlPath)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // CLI parser should not be called since there's no cache entry
        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify warning was logged
        mockLoggerForDetector.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No cached cargo metadata")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TestFileProcessingOrder_TomlBeforeLock_AndLockSkipped()
    {
        var rootToml = Path.Combine(Path.GetTempPath(), "project", "Cargo.toml");
        var rootLock = Path.Combine(Path.GetTempPath(), "project", "Cargo.lock");

        var callOrder = new List<string>();

        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, CancellationToken>((stream, recorder, token) =>
            {
                callOrder.Add("lock:" + stream.Location);
            })
            .ReturnsAsync(1);

        var normalizedRootDir = this.pathUtilityService.NormalizePath(Path.GetDirectoryName(rootToml));
        var metadata = new CargoMetadata
        {
            Packages = [],
            Resolve = new Resolve { Nodes = [] },
        };

        var ownershipResult = new IRustMetadataContextBuilder.OwnershipResult
        {
            ManifestToMetadata = new Dictionary<string, CargoMetadata>
            {
                [this.pathUtilityService.NormalizePath(rootToml)] = metadata,
            },
        };

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((paths, token) =>
            {
                foreach (var path in paths)
                {
                    callOrder.Add("toml:" + path);
                }
            })
            .ReturnsAsync(ownershipResult);

        this.mockCliParser
            .Setup(x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, CargoMetadata, IComponentRecorder, IReadOnlyDictionary<string, HashSet<string>>, CancellationToken>(
                (stream, recorder, metadata, parent, ownership, token) =>
                {
                    callOrder.Add("cli:" + stream.Location);
                })
            .ReturnsAsync(new IRustCliParser.ParseResult { Success = true });

        await this.DetectorTestUtility
            .WithFile("Cargo.toml", "[package]\nname = \"root\"", fileLocation: rootToml)
            .WithFile("Cargo.lock", "[[package]]\nname = \"root\"", fileLocation: rootLock)
            .ExecuteDetectorAsync();

        // Verify processing order
        callOrder.Should().HaveCountGreaterThanOrEqualTo(2);

        // First should be TOML metadata building
        callOrder[0].Should().StartWith("toml:");

        // Second should be CLI parser processing the TOML
        callOrder[1].Should().StartWith("cli:");

        // Cargo.lock should NOT be processed because the directory was marked as visited
        callOrder.Should().NotContain(c => c.Contains("lock:"));

        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestSkipLogic_SecondFileInSameDirectorySkipped()
    {
        var dir = Path.Combine(Path.GetTempPath(), "myproject");
        var lock1 = Path.Combine(dir, "Cargo.lock");
        var toml1 = Path.Combine(dir, "Cargo.toml");

        var parsedFiles = new List<string>();

        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .Callback<IComponentStream, ISingleFileComponentRecorder, CancellationToken>(
                (stream, recorder, token) =>
                {
                    parsedFiles.Add(stream.Location);
                })
            .ReturnsAsync(1);

        var metadata = new CargoMetadata
        {
            Packages = [],
            Resolve = new Resolve { Nodes = [] },
        };

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IRustMetadataContextBuilder.OwnershipResult
            {
                ManifestToMetadata = new Dictionary<string, CargoMetadata>
                {
                    [this.pathUtilityService.NormalizePath(toml1)] = metadata,
                },
            });

        this.mockCliParser
            .Setup(x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IRustCliParser.ParseResult { Success = true });

        await this.DetectorTestUtility
            .WithFile("Cargo.toml", "[package]\nname = \"test\"", fileLocation: toml1)
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"", fileLocation: lock1)
            .ExecuteDetectorAsync();

        // Cargo.toml should be processed via CLI parser
        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                It.Is<IComponentStream>(s => s.Location == toml1),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Cargo.lock should be skipped because directory was marked as visited
        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.Is<IComponentStream>(s => s.Location == lock1),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestWorkspaceOnlyToml_NotSkippedEvenWhenDirectoryVisited()
    {
        var workspaceDir = Path.Combine(Path.GetTempPath(), "workspace");
        var workspaceToml = Path.Combine(workspaceDir, "Cargo.toml");
        var workspaceLock = Path.Combine(workspaceDir, "Cargo.lock");

        // First process the lock file to mark directory as visited
        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Track if workspace TOML was processed
        var workspaceTomlProcessed = false;

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.Is<IEnumerable<string>>(paths => paths.Any(p => p == workspaceToml)),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((paths, token) =>
            {
                if (paths.Contains(workspaceToml))
                {
                    workspaceTomlProcessed = true;
                }
            })
            .ReturnsAsync(new IRustMetadataContextBuilder.OwnershipResult());

        var workspaceTomlContent = @"
[workspace]
members = [""member1""]
";

        // Process files in order: lock first (marks directory as visited), then workspace TOML
        await this.DetectorTestUtility
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"", fileLocation: workspaceLock)
            .WithFile("Cargo.toml", workspaceTomlContent, fileLocation: workspaceToml)
            .ExecuteDetectorAsync();

        // Workspace-only TOML should still be processed despite directory being visited
        workspaceTomlProcessed.Should().BeTrue("Workspace-only Cargo.toml should never be skipped");

        this.mockMetadataContextBuilder.Verify(
            x => x.BuildPackageOwnershipMapAsync(
                It.Is<IEnumerable<string>>(paths => paths.Contains(workspaceToml)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /* Error Handling Tests */

    [TestMethod]
    public async Task TestFailedManifests_LoggedAndOthersContinue()
    {
        var workingToml = Path.Combine(Path.GetTempPath(), "working", "Cargo.toml");
        var failedToml = Path.Combine(Path.GetTempPath(), "failed", "Cargo.toml");
        var workingLock = Path.Combine(Path.GetTempPath(), "working", "Cargo.lock");

        var ownershipResult = new IRustMetadataContextBuilder.OwnershipResult
        {
            FailedManifests = [this.pathUtilityService.NormalizePath(failedToml)],
            ManifestToMetadata = new Dictionary<string, CargoMetadata>
            {
                [this.pathUtilityService.NormalizePath(workingToml)] = new CargoMetadata
                {
                    Packages = [],
                    Resolve = new Resolve { Nodes = [] },
                },
            },
        };

        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownershipResult);

        this.mockCliParser
            .Setup(x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IRustCliParser.ParseResult { Success = true });

        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", "[package]\nname = \"working\"", fileLocation: workingToml)
            .WithFile("Cargo.toml", "[package]\nname = \"failed\"", fileLocation: failedToml)
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"", fileLocation: workingLock)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Working TOML should be processed
        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                It.Is<IComponentStream>(s => s.Location == workingToml),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Failed TOML should NOT have metadata, so CLI parser shouldn't be called for it
        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                    It.Is<IComponentStream>(s => s.Location == failedToml),
                    It.IsAny<ISingleFileComponentRecorder>(),
                    It.IsAny<CargoMetadata>(),
                    It.IsAny<IComponentRecorder>(),
                    It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestMetadataContextBuilder_FailsGracefully()
    {
        this.mockMetadataContextBuilder
            .Setup(x => x.BuildPackageOwnershipMapAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cargo metadata command failed: cargo not found"));

        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.toml", "[package]\nname = \"test\"")
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"")
            .ExecuteDetectorAsync();

        // Detection should continue despite metadata build failure
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Lock parser should still be called
        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TestSbomOnlyMode_ProcessesSbomFilesOnly()
    {
        var sbomContent = /*lang=json,strict*/ @"
{
  ""version"": 1,
  ""root"": 0,
  ""crates"": []
}";

        this.mockSbomParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("test.cargo-sbom.json", sbomContent)
            .WithFile("Cargo.toml", "[package]\nname = \"test\"")
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"")
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Verify CLI and lock parsers were NOT called in SBOM_ONLY mode
        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestCargoLockMode_RecordsLockfileVersion()
    {
        this.mockCargoLockParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"")
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Telemetry should contain lockfile version
        result.AdditionalTelemetryDetails.Should().ContainKey("LockfileVersion");
    }

    [TestMethod]
    public async Task TestMixedSbomAndLockFiles_SbomTakesPrecedence()
    {
        var sbomContent = /*lang=json,strict*/ @"
{
  ""version"": 1,
  ""root"": 0,
  ""crates"": []
}";

        this.mockSbomParser
            .Setup(x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await this.DetectorTestUtility
            .WithFile("test.cargo-sbom.json", sbomContent)
            .WithFile("Cargo.lock", "[[package]]\nname = \"test\"")
            .WithFile("Cargo.toml", "[package]\nname = \"test\"")
            .ExecuteDetectorAsync();

        // Only SBOM mode should be active (SBOM_ONLY mode)
        this.mockCargoLockParser.Verify(
            x => x.ParseAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        this.mockCliParser.Verify(
            x => x.ParseFromMetadataAsync(
                It.IsAny<IComponentStream>(),
                It.IsAny<ISingleFileComponentRecorder>(),
                It.IsAny<CargoMetadata>(),
                It.IsAny<IComponentRecorder>(),
                It.IsAny<IReadOnlyDictionary<string, HashSet<string>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
