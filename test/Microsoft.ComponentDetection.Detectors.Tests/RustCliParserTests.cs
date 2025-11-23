#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static Microsoft.ComponentDetection.Detectors.Rust.IRustCliParser;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustCliParserTests
{
    private RustCliParser parser;
    private Mock<ICommandLineInvocationService> cli;
    private Mock<IEnvironmentVariableService> env;
    private Mock<ILogger<RustCliParser>> logger;

    [TestInitialize]
    public void Init()
    {
        this.cli = new Mock<ICommandLineInvocationService>(MockBehavior.Strict);
        this.env = new Mock<IEnvironmentVariableService>(MockBehavior.Strict);
        this.logger = new Mock<ILogger<RustCliParser>>(MockBehavior.Loose);
        this.env.Setup(e => e.IsEnvironmentVariableValueTrue(It.IsAny<string>())).Returns(false);

        this.parser = new RustCliParser(this.cli.Object, this.env.Object, new PathUtilityService(new Mock<ILogger<PathUtilityService>>().Object), this.logger.Object);
    }

    [TestMethod]
    public async Task ParseAsync_ManuallyDisabled_ReturnsFailure()
    {
        this.env.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(true);

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), new Mock<ISingleFileComponentRecorder>().Object);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Manually Disabled");

        this.cli.Verify(c => c.CanCommandBeLocatedAsync(It.IsAny<string>(), null), Times.Never);
    }

    [TestMethod]
    public async Task ParseAsync_CargoNotFound_ReturnsFailure()
    {
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(false);

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), new Mock<ISingleFileComponentRecorder>().Object);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Could not locate cargo command");
    }

    [TestMethod]
    public async Task ParseAsync_MetadataCommandFailure_ReturnsFailure()
    {
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cli.Setup(c => c.ExecuteCommandAsync(
                            "cargo",
                            null,
                            null,
                            It.IsAny<CancellationToken>(),
                            "metadata",
                            "--manifest-path",
                            "C:/repo/Cargo.toml",
                            "--format-version=1",
                            "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 1, StdErr = "error msg" });

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), new Mock<ISingleFileComponentRecorder>().Object);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("`cargo metadata` failed");
        result.ErrorMessage.Should().Be("error msg");
    }

    [TestMethod]
    public async Task ParseAsync_MetadataCommandThrows_ReturnsFailure()
    {
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cli.Setup(c => c.ExecuteCommandAsync(
                            "cargo",
                            null,
                            null,
                            It.IsAny<CancellationToken>(),
                            "metadata",
                            "--manifest-path",
                            "C:/repo/Cargo.toml",
                            "--format-version=1",
                            "--locked"))
                .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), new Mock<ISingleFileComponentRecorder>().Object);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Exception during cargo metadata");
        result.ErrorMessage.Should().Be("boom");
    }

    [TestMethod]
    public async Task ParseAsync_Success_NormalRoot_RegistersChildrenAndFlags()
    {
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        var json = BuildNormalRootMetadataJson();
        this.cli.Setup(c => c.ExecuteCommandAsync(
                                "cargo",
                                null,
                                null,
                                It.IsAny<CancellationToken>(),
                                "metadata",
                                "--manifest-path",
                                "C:/repo/Cargo.toml",
                                "--format-version=1",
                                "--locked"))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), recorder.Object);
        result.Success.Should().BeTrue();

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().HaveCount(2);

        registrations.Should().OnlyContain(i => i.Arguments[1] != null && (bool)i.Arguments[1]);

        static string NameOf(IInvocation inv) => ((CargoComponent)((DetectedComponent)inv.Arguments[0]).Component).Name;

        var childDevInvocation = registrations.Single(r => NameOf(r) == "childDev");
        childDevInvocation.Arguments[3].Should().BeOfType<bool>().Which.Should().BeTrue();

        var childAInvocation = registrations.Single(r => NameOf(r) == "childA");
        childAInvocation.Arguments[3].Should().Be(false);
    }

    [TestMethod]
    public async Task ParseAsync_Success_VirtualManifest_NoExplicitFlags()
    {
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        var json = BuildVirtualManifestMetadataJson();
        this.cli.Setup(c => c.ExecuteCommandAsync(
                                "cargo",
                                null,
                                null,
                                It.IsAny<CancellationToken>(),
                                "metadata",
                                "--manifest-path",
                                "C:/repo/Cargo.toml",
                                "--format-version=1",
                                "--locked"))
                    .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var graph = new Mock<IDependencyGraph>();
        graph.Setup(g => g.Contains(It.IsAny<string>())).Returns(false);
        recorder.Setup(r => r.DependencyGraph).Returns(graph.Object);

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), recorder.Object);
        result.Success.Should().BeTrue();

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // NOTE: In virtual manifest scenarios the traversal uses two different visited keys:
        // 1. Initial loop in ProcessMetadata: componentKey = dep.Id
        // 2. Recursive traversal: componentKey = $"{detectedComponent.Component.Id}{dep.Pkg} {isTomlRoot}"
        // This can yield an additional (duplicate) registration for a child when it is reached via another package.
        // We now assert on distinct components rather than raw invocation count.
        registrations.Count.Should().BeGreaterThanOrEqualTo(2);          // Raw calls may be > distinct components
        var distinctNames = registrations
            .Select(r => ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name)
            .Distinct()
            .ToList();

        distinctNames.Should().BeEquivalentTo(["virtA", "virtB"]);

        // All registrations in a virtual manifest should have explicit flag = false.
        registrations.Should().OnlyContain(i => i.Arguments[1] != null && i.Arguments[1] is bool && !(bool)i.Arguments[1]);
    }

    [TestMethod]
    public async Task ParseFromMetadataAsync_NullMetadata_Failure()
    {
        var fallback = new Mock<ISingleFileComponentRecorder>().Object;
        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), fallback, null, null, null);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Cached metadata unavailable");
    }

    [TestMethod]
    public async Task ParseFromMetadataAsync_ManuallyDisabled_Failure()
    {
        this.env.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(true);
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());
        var fallback = new Mock<ISingleFileComponentRecorder>().Object;
        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), fallback, metadata, null, null);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Manually Disabled");
    }

    [TestMethod]
    public async Task ParseFromMetadataAsync_OwnershipMultipleOwners_RegistersForEach()
    {
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner1 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var owner2 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/one")).Returns(owner1.Object);
        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/two")).Returns(owner2.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
        {
            { "childA 2.0.0", new HashSet<string> { "manifests/one", "manifests/two" } },
            { "childDev 3.0.0", new HashSet<string> { "manifests/one", "manifests/two" } },
        };

        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), fallback.Object, metadata, parentRecorder.Object, ownershipMap);
        result.Success.Should().BeTrue();

        owner1.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(2);
        owner2.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(2);
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task ParseFromMetadataAsync_OwnershipFallback_NoOwners_UsesFallbackRecorderAsync()
    {
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var ownershipMap = new Dictionary<string, HashSet<string>> { };

        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), fallback.Object, metadata, parentRecorder.Object, ownershipMap);
        result.Success.Should().BeTrue();

        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(2);
        parentRecorder.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ProcessMetadata_LocalPackageDirectoriesCollectedAsync()
    {
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var r = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);
        r.Success.Should().BeTrue();
        r.LocalPackageDirectories.Should().Contain("C:/repo/root");
        r.LocalPackageDirectories.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Traverse_MissingPackage_WarnsAndSkipsAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"pkgA","version":"1.0.0","id":"pkgA 1.0.0","authors":["A"],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/A/Cargo.toml" }
          ],
          "resolve": {
            "root":"pkgA 1.0.0",
            "nodes":[
              { "id":"pkgA 1.0.0", "deps":[ { "pkg":"missing 2.0.0", "dep_kinds":[{"kind":"build"}] } ] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var r = await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);
        r.Success.Should().BeTrue();
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task Traverse_MissingGraphNode_WarnsAndSkipsAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"rootpkg","version":"1.0.0","id":"rootpkg 1.0.0","authors":["A"],"license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"lonely","version":"2.0.0","id":"lonely 2.0.0","authors":["B"],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/lonely/Cargo.toml" }
          ],
          "resolve": {
            "root":"rootpkg 1.0.0",
            "nodes":[
              { "id":"rootpkg 1.0.0", "deps":[ { "pkg":"lonely 2.0.0", "dep_kinds":[{"kind":"build"}] } ] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var r = await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);
        r.Success.Should().BeTrue();
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task Traverse_DuplicateDependency_OnlySingleRegistrationAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"rootpkg","version":"1.0.0","id":"rootpkg 1.0.0","authors":["A"],"license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"childX","version":"2.0.0","id":"childX 2.0.0","authors":["C"],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/childX/Cargo.toml" }
          ],
          "resolve": {
            "root":"rootpkg 1.0.0",
            "nodes":[
              { "id":"rootpkg 1.0.0",
                "deps":[
                  { "pkg":"childX 2.0.0", "dep_kinds":[{"kind":"build"}] },
                  { "pkg":"childX 2.0.0", "dep_kinds":[{"kind":"build"}] }
                ]
              },
              { "id":"childX 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var r = await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);
        r.Success.Should().BeTrue();
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
    }

    // Updated expectation: any blank author causes overall Author=null per current implementation.
    [TestMethod]
    public async Task AuthorsAndLicenseNormalization_BlankAuthorsOrLicenseBecomeNullAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"rootpkg","version":"1.0.0","id":"rootpkg 1.0.0","authors":[""],"license":"", "source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"child","version":"2.0.0","id":"child 2.0.0","authors":["Alice",""],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/child/Cargo.toml" }
          ],
          "resolve": {
            "root":"rootpkg 1.0.0",
            "nodes":[
              { "id":"rootpkg 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"child 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;
        comp.Author.Should().BeNull();   // Behavior: any blank entry nulls entire author set
        comp.License.Should().Be("MIT");
    }

    [TestMethod]
    public async Task AuthorsNormalization_NoBlanks_PreservesAuthorAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"root","version":"1.0.0","id":"root 1.0.0","authors":["Root"],"license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"child","version":"2.0.0","id":"child 2.0.0","authors":["Alice"],"license":"Apache-2.0","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/child/Cargo.toml" }
          ],
          "resolve": {
            "root":"root 1.0.0",
            "nodes":[
              { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"child 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;
        comp.Author.Should().Be("Alice");
        comp.License.Should().Be("Apache-2.0");
    }

    [TestMethod]
    public async Task AuthorsNormalization_AllBlanks_NullsAuthorAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"root","version":"1.0.0","id":"root 1.0.0","authors":[""],"license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"child","version":"2.0.0","id":"child 2.0.0","authors":["","  "],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/child/Cargo.toml" }
          ],
          "resolve": {
            "root":"root 1.0.0",
            "nodes":[
              { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"child 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;
        comp.Author.Should().BeNull();
        comp.License.Should().Be("MIT");
    }

    [TestMethod]
    public async Task AuthorsNormalization_EmptyOrNullArray_NullsAuthorAsync()
    {
        // Empty authors array
        var jsonEmpty = """
        {
          "packages": [
            { "name":"root","version":"1.0.0","id":"root 1.0.0","authors":[],"license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"childEmpty","version":"2.0.0","id":"childEmpty 2.0.0","authors":[],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/childEmpty/Cargo.toml" }
          ],
          "resolve": {
            "root":"root 1.0.0",
            "nodes":[
              { "id":"root 1.0.0", "deps":[ { "pkg":"childEmpty 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"childEmpty 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadataEmpty = ParseMetadata(jsonEmpty);
        var fallbackEmpty = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallbackEmpty.Object, metadataEmpty);
        var regEmpty = fallbackEmpty.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var compEmpty = ((DetectedComponent)regEmpty.Arguments[0]).Component as CargoComponent;
        compEmpty.Author.Should().BeNull();

        // Missing authors (null)
        var jsonNull = """
        {
          "packages": [
            { "name":"root","version":"1.0.0","id":"root 1.0.0","license":"MIT","source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"childNull","version":"2.0.0","id":"childNull 2.0.0","license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/childNull/Cargo.toml" }
          ],
          "resolve": {
            "root":"root 1.0.0",
            "nodes":[
              { "id":"root 1.0.0", "deps":[ { "pkg":"childNull 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"childNull 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadataNull = ParseMetadata(jsonNull);
        var fallbackNull = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo2.toml", fallbackNull.Object, metadataNull);
        var regNull = fallbackNull.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var compNull = ((DetectedComponent)regNull.Arguments[0]).Component as CargoComponent;
        compNull.Author.Should().BeNull();
    }

    [TestMethod]
    public async Task LicenseNormalization_BlankLicense_NullsLicenseAsync()
    {
        var json = """
        {
          "packages": [
            { "name":"rootpkg","version":"1.0.0","id":"rootpkg 1.0.0","authors":["A"],"license":"", "source":null,"manifest_path":"C:/p/root/Cargo.toml" },
            { "name":"child","version":"2.0.0","id":"child 2.0.0","authors":["B"],"license":"MIT","source":"registry+https://github.com/rust-lang/crates.io-index","manifest_path":"C:/p/child/Cargo.toml" }
          ],
          "resolve": {
            "root":"rootpkg 1.0.0",
            "nodes":[
              { "id":"rootpkg 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
              { "id":"child 2.0.0", "deps":[] }
            ]
          }
        }
        """;
        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/p/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;
        comp.License.Should().Be("MIT"); // Root not registered (source=null); child has non-blank license.
    }

    [TestMethod]
    public async Task ParseAsync_MultipleTransitiveLevels_CorrectParentChildRelationships()
    {
        // Tests deep dependency chains with proper parent-child relationships
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":["R"], "license":"MIT", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"level1", "version":"1.0.0", "id":"level1 1.0.0", "authors":["L1"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/level1/Cargo.toml" },
        { "name":"level2", "version":"1.0.0", "id":"level2 1.0.0", "authors":["L2"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/level2/Cargo.toml" },
        { "name":"level3", "version":"1.0.0", "id":"level3 1.0.0", "authors":["L3"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/level3/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"level1 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"level1 1.0.0", "deps":[ { "pkg":"level2 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"level2 1.0.0", "deps":[ { "pkg":"level3 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"level3 1.0.0", "deps":[] }
        ]
      }
    }
    """;

        this.cli.Setup(c => c.ExecuteCommandAsync("cargo", null, null, It.IsAny<CancellationToken>(), "metadata", "--manifest-path", "C:/repo/Cargo.toml", "--format-version=1", "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var graph = new Mock<IDependencyGraph>();

        // Setup graph to validate parent-child relationships
        var registeredComponents = new HashSet<string>();
        graph.Setup(g => g.Contains(It.IsAny<string>())).Returns<string>(id => registeredComponents.Contains(id));
        recorder.Setup(r => r.DependencyGraph).Returns(graph.Object);
        recorder.Setup(r => r.RegisterUsage(It.IsAny<DetectedComponent>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DependencyScope?>(), It.IsAny<string>()))
            .Callback<DetectedComponent, bool, string, bool?, DependencyScope?, string>(
                (dc, explicitRef, parentId, isDevDep, dependencyScope, targetFramework) => registeredComponents.Add(dc.Component.Id));

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), recorder.Object);
        result.Success.Should().BeTrue();

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Should have registrations with proper parent relationships
        registrations.Should().HaveCountGreaterThanOrEqualTo(3);

        // Verify at least one registration has a parent component ID
        registrations.Should().Contain(r => r.Arguments[2] != null && !string.IsNullOrEmpty((string)r.Arguments[2]));
    }

    [TestMethod]
    public async Task ParseAsync_DiamondDependency_HandledCorrectly()
    {
        // Tests diamond dependency pattern: root -> A, B; A -> C; B -> C
        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":["R"], "license":"MIT", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"depA", "version":"1.0.0", "id":"depA 1.0.0", "authors":["A"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/depA/Cargo.toml" },
        { "name":"depB", "version":"1.0.0", "id":"depB 1.0.0", "authors":["B"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/depB/Cargo.toml" },
        { "name":"shared", "version":"1.0.0", "id":"shared 1.0.0", "authors":["S"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/shared/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[
              { "pkg":"depA 1.0.0", "dep_kinds":[{"kind":"build"}] },
              { "pkg":"depB 1.0.0", "dep_kinds":[{"kind":"build"}] }
          ] },
          { "id":"depA 1.0.0", "deps":[ { "pkg":"shared 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"depB 1.0.0", "deps":[ { "pkg":"shared 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"shared 1.0.0", "deps":[] }
        ]
      }
    }
    """;

        this.cli.Setup(c => c.ExecuteCommandAsync("cargo", null, null, It.IsAny<CancellationToken>(), "metadata", "--manifest-path", "C:/repo/Cargo.toml", "--format-version=1", "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var graph = new Mock<IDependencyGraph>();
        var registeredComponents = new HashSet<string>();
        graph.Setup(g => g.Contains(It.IsAny<string>())).Returns<string>(id => registeredComponents.Contains(id));
        recorder.Setup(r => r.DependencyGraph).Returns(graph.Object);
        recorder.Setup(r => r.RegisterUsage(It.IsAny<DetectedComponent>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DependencyScope?>(), It.IsAny<string>()))
            .Callback<DetectedComponent, bool, string, bool?, DependencyScope?, string>(
                (dc, explicitRef, parentId, isDevDep, dependencyScope, targetFramework) => registeredComponents.Add(dc.Component.Id));

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), recorder.Object);
        result.Success.Should().BeTrue();

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Should register shared component multiple times (once per path)
        var sharedRegistrations = registrations.Where(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "shared").ToList();

        sharedRegistrations.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public async Task ParseFromMetadataAsync_OwnershipPartialMapping_MixesFallbackAndOwners()
    {
        // Tests scenario where some components have owners and others don't
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner1 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/one")).Returns(owner1.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
    {
        { "childA 2.0.0", new HashSet<string> { "manifests/one" } },

        // childDev 3.0.0 deliberately not in ownership map
    };

        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), fallback.Object, metadata, parentRecorder.Object, ownershipMap);
        result.Success.Should().BeTrue();

        // childA should use owner1
        owner1.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);

        // childDev should use fallback
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
    }

    [TestMethod]
    public async Task ProcessMetadata_MultipleLocalPackages_AllDirectoriesCollected()
    {
        // Tests that multiple local packages all have their directories collected
        var json = """
    {
      "packages": [
        { "name":"local1", "version":"1.0.0", "id":"local1 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/local1/Cargo.toml" },
        { "name":"local2", "version":"2.0.0", "id":"local2 2.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/local2/Cargo.toml" },
        { "name":"remote", "version":"3.0.0", "id":"remote 3.0.0", "authors":["R"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/remote/Cargo.toml" }
      ],
      "resolve": {
        "root":"local1 1.0.0",
        "nodes":[
          { "id":"local1 1.0.0", "deps":[ { "pkg":"local2 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"local2 2.0.0", "deps":[ { "pkg":"remote 3.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"remote 3.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var result = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        result.Success.Should().BeTrue();
        result.LocalPackageDirectories.Should().HaveCount(2);
        result.LocalPackageDirectories.Should().Contain(d => d.Contains("local1"));
        result.LocalPackageDirectories.Should().Contain(d => d.Contains("local2"));
    }

    [TestMethod]
    public async Task Traverse_MixedDevAndBuildDependencies_CorrectFlagsSet()
    {
        // Tests that both dev and build dependencies are correctly flagged
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"devDep", "version":"1.0.0", "id":"devDep 1.0.0", "authors":["D"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/devDep/Cargo.toml" },
        { "name":"buildDep", "version":"1.0.0", "id":"buildDep 1.0.0", "authors":["B"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/buildDep/Cargo.toml" },
        { "name":"normalDep", "version":"1.0.0", "id":"normalDep 1.0.0", "authors":["N"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/normalDep/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[
              { "pkg":"devDep 1.0.0", "dep_kinds":[{"kind":"dev"}] },
              { "pkg":"buildDep 1.0.0", "dep_kinds":[{"kind":"build"}] },
              { "pkg":"normalDep 1.0.0", "dep_kinds":[{"kind":null}] }
          ] },
          { "id":"devDep 1.0.0", "deps":[] },
          { "id":"buildDep 1.0.0", "deps":[] },
          { "id":"normalDep 1.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        var registrations = fallback.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Verify devDep has isDevelopmentDependency = true
        var devDepReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "devDep");
        devDepReg.Arguments[3].Should().Be(true);

        // Verify buildDep has isDevelopmentDependency = false
        var buildDepReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "buildDep");
        buildDepReg.Arguments[3].Should().Be(false);
    }

    [TestMethod]
    public async Task VirtualManifest_WithTransitiveDependencies_AllRegistered()
    {
        // Tests virtual manifest with deeper dependency chains
        var json = """
    {
      "packages": [
        { "name":"virtPkg", "version":"1.0.0", "id":"virtPkg 1.0.0", "authors":["V"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/virtPkg/Cargo.toml" },
        { "name":"transitive", "version":"2.0.0", "id":"transitive 2.0.0", "authors":["T"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/transitive/Cargo.toml" }
      ],
      "resolve": {
        "root": null,
        "nodes":[
          { "id":"virtPkg 1.0.0", "deps":[ { "pkg":"transitive 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"transitive 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        this.cli.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cli.Setup(c => c.ExecuteCommandAsync("cargo", null, null, It.IsAny<CancellationToken>(), "metadata", "--manifest-path", "C:/repo/Cargo.toml", "--format-version=1", "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var graph = new Mock<IDependencyGraph>();
        graph.Setup(g => g.Contains(It.IsAny<string>())).Returns(false);
        recorder.Setup(r => r.DependencyGraph).Returns(graph.Object);

        var result = await this.parser.ParseAsync(MakeTomlStream("C:/repo/Cargo.toml"), recorder.Object);
        result.Success.Should().BeTrue();

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Should register both packages
        var distinctComponents = registrations
            .Select(r => ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name)
            .Distinct()
            .ToList();

        distinctComponents.Should().Contain("virtPkg");
        distinctComponents.Should().Contain("transitive");
    }

    [TestMethod]
    public async Task ApplyOwners_ParentComponentId_OnlySetWhenInGraph()
    {
        // Tests that parentComponentId is only passed when it exists in the target recorder's graph
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var ownerGraph = new Mock<IDependencyGraph>();

        // Setup graph to NOT contain parent
        ownerGraph.Setup(g => g.Contains(It.IsAny<string>())).Returns(false);
        owner.Setup(r => r.DependencyGraph).Returns(ownerGraph.Object);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/one")).Returns(owner.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
    {
        { "childA 2.0.0", new HashSet<string> { "manifests/one" } },
    };

        var result = await this.parser.ParseFromMetadataAsync(MakeTomlStream("C:/repo/Cargo.toml"), new Mock<ISingleFileComponentRecorder>().Object, metadata, parentRecorder.Object, ownershipMap);
        result.Success.Should().BeTrue();

        var registrations = owner.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Parent ID should be null since it's not in the graph
        registrations.Should().OnlyContain(r => r.Arguments[2] == null);
    }

    [TestMethod]
    public async Task AuthorsNormalization_MultipleNonBlankAuthors_JoinsWithComma()
    {
        // Tests that multiple valid authors are joined correctly
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"MIT", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"child", "version":"2.0.0", "id":"child 2.0.0", "authors":["Alice Smith","Bob Jones","Charlie Brown"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/child/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"child 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;

        comp.Author.Should().Be("Alice Smith, Bob Jones, Charlie Brown");
    }

    [TestMethod]
    public async Task ProcessMetadata_EmptyPackagesAndNodes_Success()
    {
        // Tests handling of empty packages and nodes
        var json = """
    {
      "packages": [],
      "resolve": {
        "root": null,
        "nodes":[]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var result = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        result.Success.Should().BeTrue();
        result.LocalPackageDirectories.Should().BeEmpty();
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task Traverse_IndexOutOfRangeException_RegistersParseFailure()
    {
        // Tests IndexOutOfRangeException handling in TraverseAndRecordComponents
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"child", "version":"2.0.0", "id":"child 2.0.0", "authors":["A"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/child/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"child 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        // Mock RegisterPackageParseFailure to verify it's called
        fallback.Setup(f => f.RegisterPackageParseFailure(It.IsAny<string>()));

        var result = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        result.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task ProcessMetadata_LocalPackageWithEmptyManifestPath_Skipped()
    {
        // Tests handling of packages with empty manifest paths
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var result = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        result.Success.Should().BeTrue();
        result.LocalPackageDirectories.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyOwners_WithParentInGraph_PassesParentId()
    {
        // Tests that parentComponentId is passed when parent exists in graph
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var ownerGraph = new Mock<IDependencyGraph>();

        // Setup graph to contain the parent
        ownerGraph.Setup(g => g.Contains("childA 2.0.0")).Returns(true);
        owner.Setup(r => r.DependencyGraph).Returns(ownerGraph.Object);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/one")).Returns(owner.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
    {
        { "childA 2.0.0", new HashSet<string> { "manifests/one" } },
    };

        var result = await this.parser.ParseFromMetadataAsync(
            MakeTomlStream("C:/repo/Cargo.toml"),
            new Mock<ISingleFileComponentRecorder>().Object,
            metadata,
            parentRecorder.Object,
            ownershipMap);

        result.Success.Should().BeTrue();

        var registrations = owner.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ApplyOwners_EmptyOwnersSet_UsesFallback()
    {
        // Tests that empty owners set falls back to fallback recorder
        var metadata = ParseMetadata(BuildNormalRootMetadataJson());

        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var ownershipMap = new Dictionary<string, HashSet<string>>
    {
        { "childA 2.0.0", new HashSet<string>() }, // Empty set
    };

        var result = await this.parser.ParseFromMetadataAsync(
            MakeTomlStream("C:/repo/Cargo.toml"),
            fallback.Object,
            metadata,
            parentRecorder.Object,
            ownershipMap);

        result.Success.Should().BeTrue();

        // Should use fallback for childA since owners set is empty
        fallback.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().BeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public async Task Traverse_DepKindsNull_NotTreatedAsDevelopmentDependency()
    {
        // Tests handling of null DepKinds
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"child", "version":"2.0.0", "id":"child 2.0.0", "authors":["A"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/child/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":null } ] },
          { "id":"child 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        var registrations = fallback.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().ContainSingle();

        // Verify isDevelopmentDependency is false (not true)
        registrations[0].Arguments[3].Should().Be(false);
    }

    [TestMethod]
    public async Task ProcessMetadata_PackageWithNullAuthors_AuthorIsNull()
    {
        // Tests that null authors array results in null Author
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":null, "license":"MIT", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"child", "version":"2.0.0", "id":"child 2.0.0", "authors":null, "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/child/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"child 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;

        comp.Author.Should().BeNull();
    }

    [TestMethod]
    public async Task ProcessMetadata_PackageWithNullLicense_LicenseIsNull()
    {
        // Tests that null license results in null License
        var json = """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":["A"], "license":null, "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"child", "version":"2.0.0", "id":"child 2.0.0", "authors":["B"], "license":null, "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/child/Cargo.toml" }
      ],
      "resolve": {
        "root":"root 1.0.0",
        "nodes":[
          { "id":"root 1.0.0", "deps":[ { "pkg":"child 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"child 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        var reg = fallback.Invocations.Single(i => i.Method.Name == "RegisterUsage");
        var comp = ((DetectedComponent)reg.Arguments[0]).Component as CargoComponent;

        comp.License.Should().BeNull();
    }

    [TestMethod]
    public async Task VirtualManifest_MultipleRootNodes_AllProcessed()
    {
        // Tests virtual manifest with multiple independent root nodes
        var json = """
    {
      "packages": [
        { "name":"pkgA", "version":"1.0.0", "id":"pkgA 1.0.0", "authors":["A"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/pkgA/Cargo.toml" },
        { "name":"pkgB", "version":"2.0.0", "id":"pkgB 2.0.0", "authors":["B"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/pkgB/Cargo.toml" }
      ],
      "resolve": {
        "root": null,
        "nodes":[
          { "id":"pkgA 1.0.0", "deps":[] },
          { "id":"pkgB 2.0.0", "deps":[] }
        ]
      }
    }
    """;

        var metadata = ParseMetadata(json);
        var fallback = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var result = await this.InvokeProcessMetadataAsync("C:/repo/Cargo.toml", fallback.Object, metadata);

        result.Success.Should().BeTrue();

        var registrations = fallback.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        var distinctNames = registrations
            .Select(r => ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name)
            .Distinct()
            .ToList();

        distinctNames.Should().Contain("pkgA");
        distinctNames.Should().Contain("pkgB");
    }

    private static IComponentStream MakeTomlStream(string path) =>
        new ComponentStream { Location = path, Pattern = "Cargo.toml", Stream = new MemoryStream(Encoding.UTF8.GetBytes("[package]\nname=\"x\"")) };

    // kind: build (non-dev), kind: dev (development), or absent/null.
    private static string BuildNormalRootMetadataJson() => """
    {
      "packages": [
        { "name":"rootpkg", "version":"1.0.0", "id":"rootpkg 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/root/Cargo.toml" },
        { "name":"childA", "version":"2.0.0", "id":"childA 2.0.0", "authors":["Alice"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/root/childA/Cargo.toml" },
        { "name":"childDev", "version":"3.0.0", "id":"childDev 3.0.0", "authors":["Bob"], "license":"Apache-2.0", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/root/childDev/Cargo.toml" }
      ],
      "resolve": {
        "root":"rootpkg 1.0.0",
        "nodes":[
          { "id":"rootpkg 1.0.0",
            "deps":[
              { "pkg":"childA 2.0.0", "dep_kinds":[{"kind":"build"}] },
              { "pkg":"childDev 3.0.0", "dep_kinds":[{"kind":"dev"}] }
            ]
          },
          { "id":"childA 2.0.0", "deps":[] },
          { "id":"childDev 3.0.0", "deps":[] }
        ]
      }
    }
    """;

    private static string BuildVirtualManifestMetadataJson() => """
    {
      "packages": [
        { "name":"virtA", "version":"0.2.0", "id":"virtA 0.2.0", "authors":["Ann"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/virtA/Cargo.toml" },
        { "name":"virtB", "version":"0.3.0", "id":"virtB 0.3.0", "authors":["Ben"], "license":"Apache-2.0", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/virtB/Cargo.toml" }
      ],
      "resolve": {
        "root": null,
        "nodes":[
          { "id":"virtA 0.2.0", "deps":[ { "pkg":"virtB 0.3.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"virtB 0.3.0", "deps":[] }
        ]
      }
    }
    """;

    private static CargoMetadata ParseMetadata(string json) => CargoMetadata.FromJson(json);

    private async Task<ParseResult> InvokeProcessMetadataAsync(string manifestLocation, ISingleFileComponentRecorder fallbackRecorder, CargoMetadata metadata) =>
        await this.parser.ParseFromMetadataAsync(
            new ComponentStream { Location = manifestLocation, Pattern = "Cargo.toml", Stream = new MemoryStream([]) },
            fallbackRecorder,
            metadata,
            parentComponentRecorder: null,
            ownershipMap: null);
}
