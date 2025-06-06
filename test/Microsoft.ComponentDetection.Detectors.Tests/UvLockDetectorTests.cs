namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Uv;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        graphs.Should().ContainKey("uv.lock");
        var graph = graphs["uv.lock"];
        var fooId = new PipComponent("foo", "1.2.3").Id;
        var barId = new PipComponent("bar", "4.5.6").Id;
        graph.GetComponents().Should().BeEquivalentTo([fooId, barId]);
        graph.GetDependenciesForComponent(fooId).Should().BeEmpty();
        graph.GetDependenciesForComponent(barId).Should().BeEmpty();
        graph.IsComponentExplicitlyReferenced(fooId).Should().BeTrue();
        graph.IsComponentExplicitlyReferenced(barId).Should().BeTrue();
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
    public async Task TestUvLockDetectorWithMultipleLockFiles_FindsAllComponentsAndGraphAsync()
    {
        var uvLock1 = @"
[[package]]
name = 'foo'
version = '1.2.3'
";
        var uvLock2 = @"
[[package]]
name = 'bar'
version = '4.5.6'
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("uv.lock", uvLock1)
            .WithFile("uv2.lock", uvLock2, ["uv.lock"])
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);
        detectedComponents.Select(x => ((PipComponent)x.Component).Name).Should().BeEquivalentTo(["foo", "bar"]);

        // Validate both graphs
        var graphs = componentRecorder.GetDependencyGraphsByLocation();
        graphs.Should().ContainKey("uv.lock");
        graphs.Should().ContainKey("uv2.lock");
        var fooId = new PipComponent("foo", "1.2.3").Id;
        var barId = new PipComponent("bar", "4.5.6").Id;
        graphs["uv.lock"].GetComponents().Should().Contain(fooId);
        graphs["uv2.lock"].GetComponents().Should().Contain(barId);
        graphs["uv.lock"].IsComponentExplicitlyReferenced(fooId).Should().BeTrue();
        graphs["uv2.lock"].IsComponentExplicitlyReferenced(barId).Should().BeTrue();
    }
}
