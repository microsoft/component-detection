namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustMetadataContextBuilderTests
{
    private RustMetadataContextBuilder builder;
    private Mock<ILogger<RustMetadataContextBuilder>> logger;
    private Mock<ICommandLineInvocationService> cliService;
    private Mock<IEnvironmentVariableService> envVarService;

    [TestInitialize]
    public void Init()
    {
        this.logger = new Mock<ILogger<RustMetadataContextBuilder>>(MockBehavior.Loose);
        this.cliService = new Mock<ICommandLineInvocationService>(MockBehavior.Strict);
        this.envVarService = new Mock<IEnvironmentVariableService>(MockBehavior.Strict);

        this.builder = new RustMetadataContextBuilder(
            this.logger.Object,
            this.cliService.Object,
            new PathUtilityService(new Mock<ILogger<PathUtilityService>>().Object),
            this.envVarService.Object);
    }

    private static string BuildSimpleMetadataJson(string rootManifest, string rootId) => $$"""
    {
      "packages": [
        { "name":"rootpkg", "version":"1.0.0", "id":"{{rootId}}", "authors":[""], "license":"", "source":null, "manifest_path":"{{rootManifest}}" },
        { "name":"dep1", "version":"2.0.0", "id":"dep1 2.0.0", "authors":["A"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/dep1/Cargo.toml" }
      ],
      "resolve": {
        "root":"{{rootId}}",
        "nodes":[
          { "id":"{{rootId}}", "deps":[ { "pkg":"dep1 2.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"dep1 2.0.0", "deps":[] }
        ]
      }
    }
    """;

    private static string BuildWorkspaceMetadataJson() => """
    {
      "packages": [
        { "name":"workspace", "version":"0.1.0", "id":"workspace 0.1.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/Cargo.toml" },
        { "name":"member1", "version":"0.2.0", "id":"member1 0.2.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/member1/Cargo.toml" },
        { "name":"member2", "version":"0.3.0", "id":"member2 0.3.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/member2/Cargo.toml" },
        { "name":"shared", "version":"1.0.0", "id":"shared 1.0.0", "authors":["S"], "license":"MIT", "source":"registry+https://github.com/rust-lang/crates.io-index", "manifest_path":"C:/repo/shared/Cargo.toml" }
      ],
      "resolve": {
        "root":"workspace 0.1.0",
        "nodes":[
          { "id":"workspace 0.1.0", "deps":[] },
          { "id":"member1 0.2.0", "deps":[ { "pkg":"shared 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"member2 0.3.0", "deps":[ { "pkg":"shared 1.0.0", "dep_kinds":[{"kind":"build"}] } ] },
          { "id":"shared 1.0.0", "deps":[] }
        ]
      }
    }
    """;

    private static string BuildDiamondDependencyJson() => """
    {
      "packages": [
        { "name":"root", "version":"1.0.0", "id":"root 1.0.0", "authors":[""], "license":"", "source":null, "manifest_path":"C:/repo/Cargo.toml" },
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

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_ManuallyDisabled_ReturnsEmptyResult()
    {
        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(true);

        var result = await this.builder.BuildPackageOwnershipMapAsync(["C:/repo/Cargo.toml"]);

        result.Should().NotBeNull();
        result.PackageToTomls.Should().BeEmpty();
        result.LocalPackageManifests.Should().BeEmpty();
        result.ManifestToMetadata.Should().BeEmpty();
        result.FailedManifests.Should().BeEmpty();

        // Verify cargo was never invoked
        this.cliService.Verify(c => c.CanCommandBeLocatedAsync(It.IsAny<string>(), null), Times.Never);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_NullTomlPaths_ReturnsEmptyResult()
    {
        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);

        var result = await this.builder.BuildPackageOwnershipMapAsync(null);

        result.Should().NotBeNull();
        result.PackageToTomls.Should().BeEmpty();
        result.LocalPackageManifests.Should().BeEmpty();
        result.ManifestToMetadata.Should().BeEmpty();
        result.FailedManifests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_EmptyTomlPaths_ReturnsEmptyResult()
    {
        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);

        var result = await this.builder.BuildPackageOwnershipMapAsync([]);

        result.Should().NotBeNull();
        result.PackageToTomls.Should().BeEmpty();
        result.LocalPackageManifests.Should().BeEmpty();
        result.ManifestToMetadata.Should().BeEmpty();
        result.FailedManifests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_CargoNotFound_AddsToFailedManifests()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(false);

        var result = await this.builder.BuildPackageOwnershipMapAsync([tomlPath]);

        result.FailedManifests.Should().Contain(tomlPath);
        result.PackageToTomls.Should().BeEmpty();
        result.LocalPackageManifests.Should().BeEmpty();
        result.ManifestToMetadata.Should().BeEmpty();
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_CargoMetadataFails_AddsToFailedManifests()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 1, StdErr = "error" });

        var result = await this.builder.BuildPackageOwnershipMapAsync([tomlPath]);

        result.FailedManifests.Should().Contain(tomlPath);
        result.PackageToTomls.Should().BeEmpty();
        result.LocalPackageManifests.Should().BeEmpty();
        result.ManifestToMetadata.Should().BeEmpty();
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_SimpleDependency_BuildsOwnershipMap()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        var json = BuildSimpleMetadataJson(tomlPath, "rootpkg 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var result = await this.builder.BuildPackageOwnershipMapAsync([tomlPath]);

        result.FailedManifests.Should().BeEmpty();
        result.LocalPackageManifests.Should().Contain(tomlPath);
        result.PackageToTomls.Should().ContainKey("rootpkg 1.0.0");
        result.PackageToTomls["rootpkg 1.0.0"].Should().Contain(tomlPath);
        result.PackageToTomls.Should().ContainKey("dep1 2.0.0");
        result.PackageToTomls["dep1 2.0.0"].Should().Contain(tomlPath);
        result.ManifestToMetadata.Should().ContainKey(tomlPath);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_DuplicateManifest_ProcessedOnce()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        var json = BuildSimpleMetadataJson(tomlPath, "rootpkg 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        // Submit the same manifest twice
        var result = await this.builder.BuildPackageOwnershipMapAsync([tomlPath, tomlPath]);

        // Should only execute cargo once
        this.cliService.Verify(
            c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"),
            Times.Once);

        result.LocalPackageManifests.Should().ContainSingle();
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_WorkspaceWithMultipleMembers_PropagatesOwnership()
    {
        var workspaceToml = "C:/repo/Cargo.toml";
        var json = BuildWorkspaceMetadataJson();

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);

        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                workspaceToml,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var result = await this.builder.BuildPackageOwnershipMapAsync([workspaceToml]);

        // Workspace and both members are local
        result.LocalPackageManifests.Should().HaveCount(3);
        result.LocalPackageManifests.Should().Contain("C:/repo/Cargo.toml");
        result.LocalPackageManifests.Should().Contain("C:/repo/member1/Cargo.toml");
        result.LocalPackageManifests.Should().Contain("C:/repo/member2/Cargo.toml");

        // Shared dependency should be owned by both members
        result.PackageToTomls.Should().ContainKey("shared 1.0.0");
        result.PackageToTomls["shared 1.0.0"].Should().Contain("C:/repo/member1/Cargo.toml");
        result.PackageToTomls["shared 1.0.0"].Should().Contain("C:/repo/member2/Cargo.toml");
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_DiamondDependency_PropagatesOwnershipCorrectly()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        var json = BuildDiamondDependencyJson();

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var result = await this.builder.BuildPackageOwnershipMapAsync([tomlPath]);

        // All dependencies should be owned by root
        result.PackageToTomls.Should().ContainKey("depA 1.0.0");
        result.PackageToTomls["depA 1.0.0"].Should().Contain(tomlPath);

        result.PackageToTomls.Should().ContainKey("depB 1.0.0");
        result.PackageToTomls["depB 1.0.0"].Should().Contain(tomlPath);

        result.PackageToTomls.Should().ContainKey("shared 1.0.0");
        result.PackageToTomls["shared 1.0.0"].Should().Contain(tomlPath);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_MultipleManifests_AggregatesResults()
    {
        var toml1 = "C:/repo1/Cargo.toml";
        var toml2 = "C:/repo2/Cargo.toml";
        var json1 = BuildSimpleMetadataJson(toml1, "root1 1.0.0");
        var json2 = BuildSimpleMetadataJson(toml2, "root2 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);

        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                toml1,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json1 });

        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                toml2,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json2 });

        var result = await this.builder.BuildPackageOwnershipMapAsync([toml1, toml2]);

        result.LocalPackageManifests.Should().HaveCount(2);
        result.ManifestToMetadata.Should().HaveCount(2);
        result.PackageToTomls.Should().ContainKey("root1 1.0.0");
        result.PackageToTomls.Should().ContainKey("root2 1.0.0");

        // dep1 2.0.0 is owned by both manifests
        result.PackageToTomls.Should().ContainKey("dep1 2.0.0");
        result.PackageToTomls["dep1 2.0.0"].Should().HaveCount(2);
        result.PackageToTomls["dep1 2.0.0"].Should().Contain(toml1);
        result.PackageToTomls["dep1 2.0.0"].Should().Contain(toml2);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_MixedSuccessAndFailure_ProcessesSuccessful()
    {
        var toml1 = "C:/repo1/Cargo.toml";
        var toml2 = "C:/repo2/Cargo.toml";
        var json1 = BuildSimpleMetadataJson(toml1, "root1 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);

        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                toml1,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json1 });

        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                toml2,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 1, StdErr = "error" });

        var result = await this.builder.BuildPackageOwnershipMapAsync([toml1, toml2]);

        result.FailedManifests.Should().Contain(toml2);
        result.LocalPackageManifests.Should().Contain(toml1);
        result.ManifestToMetadata.Should().ContainKey(toml1);
        result.ManifestToMetadata.Should().NotContainKey(toml2);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_CancellationToken_PassedThrough()
    {
        var tomlPath = "C:/repo/Cargo.toml";
        var cts = new CancellationTokenSource();
        var json = BuildSimpleMetadataJson(tomlPath, "rootpkg 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                cts.Token,
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        await this.builder.BuildPackageOwnershipMapAsync([tomlPath], cts.Token);

        this.cliService.Verify(
            c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                cts.Token,
                "metadata",
                "--manifest-path",
                tomlPath,
                "--format-version=1",
                "--locked"),
            Times.Once);
    }

    [TestMethod]
    public async Task BuildPackageOwnershipMapAsync_PathNormalization_UsesNormalizedPaths()
    {
        var rawPath = "C:\\repo\\Cargo.toml";
        var normalizedPath = "C:/repo/Cargo.toml";
        var json = BuildSimpleMetadataJson(normalizedPath, "rootpkg 1.0.0");

        this.envVarService.Setup(e => e.IsEnvironmentVariableValueTrue("DisableRustCliScan")).Returns(false);
        this.cliService.Setup(c => c.CanCommandBeLocatedAsync("cargo", null)).ReturnsAsync(true);
        this.cliService.Setup(c => c.ExecuteCommandAsync(
                "cargo",
                null,
                null,
                It.IsAny<CancellationToken>(),
                "metadata",
                "--manifest-path",
                rawPath,
                "--format-version=1",
                "--locked"))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = json });

        var result = await this.builder.BuildPackageOwnershipMapAsync([rawPath]);

        // Verify normalized path is used in results
        result.LocalPackageManifests.Should().Contain(normalizedPath);
        result.ManifestToMetadata.Should().ContainKey(normalizedPath);
    }
}
