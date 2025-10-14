#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Uv;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class UvLockDetectorTests : BaseDetectorTest<UvLockComponentDetector>
{
    [TestMethod]
    public async Task TestUvLockDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithEmptyLockFile_FindsNothingAsync()
    {
        var emptyUvLock = string.Empty; // Empty TOML
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", emptyUvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithNoPackages_FindsNothingAsync()
    {
        var uvLock = "# uv.lock file\n[metadata]\nversion = '1'\n";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithMultiplePackages_FindsAllComponentsAndGraphAsync()
    {
        var uvLock = @"
[[package]]
name = 'foo'
version = '1.2.3'
[[package]]
name = 'bar'
version = '4.5.6'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);
        detectedComponents.Select(x => ((PipComponent)x.Component).Name).Should().BeEquivalentTo(["foo", "bar"]);
        detectedComponents.Select(x => ((PipComponent)x.Component).Version).Should().BeEquivalentTo(["1.2.3", "4.5.6"]);

        // Validate dependency graph structure: both are roots, no dependencies
        var graphs = componentRecorder.GetDependencyGraphsByLocation();
        var graphKey = graphs.Keys.FirstOrDefault(k => k.EndsWith("uv.lock"));
        graphKey.Should().NotBeNull();
        var graph = graphs[graphKey];
        var fooId = new PipComponent("foo", "1.2.3").Id;
        var barId = new PipComponent("bar", "4.5.6").Id;
        graph.GetComponents().Should().BeEquivalentTo([fooId, barId]);
        graph.GetDependenciesForComponent(fooId).Should().BeEmpty();
        graph.GetDependenciesForComponent(barId).Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithDependencies_RegistersDependenciesAsync()
    {
        var uvLock = @"
[[package]]
name = 'foo'
version = '1.2.3'
dependencies = [{ name = 'bar' }]
[[package]]
name = 'bar'
version = '4.5.6'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var fooId = new PipComponent("foo", "1.2.3").Id;
        var barId = new PipComponent("bar", "4.5.6").Id;

        var graphs = componentRecorder.GetDependencyGraphsByLocation();
        var graphKey = graphs.Keys.FirstOrDefault(k => k.EndsWith("uv.lock"));
        var graph = graphs[graphKey];

        graph.GetComponents().Should().BeEquivalentTo([fooId, barId]);
        graph.GetDependenciesForComponent(fooId).Should().BeEquivalentTo([barId]);
        graph.GetDependenciesForComponent(barId).Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithMissingDependency_LogsWarningAsync()
    {
        var loggerMock = new Mock<ILogger<UvLockComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(loggerMock);

        var uvLock = @"
[[package]]
name = 'foo'
version = '1.2.3'
dependencies = [{ name = 'baz' }]
[[package]]
name = 'bar'
version = '4.5.6'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .AddServiceMock(loggerMock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should log a warning for missing dependency 'baz'
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Dependency baz not found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce());
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithMalformedPackage_IgnoresInvalidAsync()
    {
        var uvLock = @"
[[package]]
name = 'foo'
[[package]]
version = '4.5.6'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestUvLockDetectorWithInvalidFile_LogsErrorAsync()
    {
        var loggerMock = new Mock<ILogger<UvLockComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(loggerMock);

        var invalidUvLock = "not a valid toml file";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", invalidUvLock)
            .AddServiceMock(loggerMock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should log an error for parse failure
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to parse uv.lock file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce());
    }

    [TestMethod]
    public async Task TestUvLockDetector_ExplicitDependencies_AreMarkedExplicit()
    {
        var uvLock = """
[[package]]
name = 'foo'
version = '1.2.3'
source = { virtual = '.' }

[package.metadata]
requires-dist = [
    { name = "bar", specifier = ">=3.9.1" },
]

[[package]]
name = 'bar'
version = '2.0.0'
""";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detected = componentRecorder.GetDetectedComponents().ToList();
        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        detected.Should().ContainSingle();
        var barId = detected.First(d => d.Component.Id.StartsWith("bar")).Component.Id;
        graph.IsComponentExplicitlyReferenced(barId).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestUvLockDetector_DevelopmentAndNonDevelopmentDependencies()
    {
        var uvLock = @"[[package]]
name = 'foo'
version = '1.2.3'
source = { virtual = '.' }
[package.metadata]
requires-dist = [
    { name = 'bar', specifier = '>=2.0.0' },
    { name = 'baz', specifier = '>=3.0.0' }
]
[package.metadata.requires-dev]
dev = [
    { name = 'devonly', specifier = '>=4.0.0' }
]
[[package]]
name = 'bar'
version = '2.0.0'
[[package]]
name = 'baz'
version = '3.0.0'
[[package]]
name = 'devonly'
version = '4.0.0'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detected = componentRecorder.GetDetectedComponents().ToList();
        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var barId = detected.First(d => d.Component.Id.StartsWith("bar ")).Component.Id;
        var bazId = detected.First(d => d.Component.Id.StartsWith("baz ")).Component.Id;
        var devonlyId = new PipComponent("devonly", "4.0.0").Id;

        // bar and baz are non-dev dependencies, devonly is a dev dependency
        graph.IsDevelopmentDependency(barId).Should().BeFalse();
        graph.IsDevelopmentDependency(bazId).Should().BeFalse();
        graph.IsDevelopmentDependency(devonlyId).Should().BeTrue();
    }
}
