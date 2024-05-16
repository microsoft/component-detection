namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pnpm;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class Pnpm6DetectorTests : BaseDetectorTest<Pnpm6ComponentDetector>
{
    public Pnpm6DetectorTests()
    {
        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.DetectorTestUtility.WithScanRequest(
            new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                null,
                null,
                new Dictionary<string, string>(),
                null,
                componentRecorder));
        this.DetectorTestUtility.AddServiceMock(new Mock<ILogger<FileComponentDetector>>());
    }

    [TestMethod]
    public async Task TestPnpmDetector_V6Async()
    {
        var yamlFile = @"
lockfileVersion: '6.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
dependencies:
  minimist:
    specifier: 1.2.8
    version: 1.2.8
packages:
  /minimist@1.2.8:
    resolution: {integrity: sha512-2yyAR8qBkN3YuheJanUpWC5U3bb5osDywNB8RzDVlDwDHbocAJveqqj1u8+SVD7jkWT4yvsHCpWqqWqAxb0zCA==}
    dev: false
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var minimist = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("minimist"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            minimist.Component.Id,
            parentComponent => parentComponent.Name == "minimist");

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.First().Should().Contain("pnpm-lock.yaml"));

        foreach (var component in detectedComponents)
        {
            component.Component.Type.Should().Be(ComponentType.Npm);
        }
    }

    [TestMethod]
    public async Task TestPnpmDetector_V6WorkspaceAsync()
    {
        var yamlFile = @"
lockfileVersion: '6.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      minimist:
        specifier: 1.2.8
        version: 1.2.8
packages:
  /minimist@1.2.8:
    resolution: {integrity: sha512-2yyAR8qBkN3YuheJanUpWC5U3bb5osDywNB8RzDVlDwDHbocAJveqqj1u8+SVD7jkWT4yvsHCpWqqWqAxb0zCA==}
    dev: false
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var minimist = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("minimist"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            minimist.Component.Id,
            parentComponent => parentComponent.Name == "minimist");

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.First().Should().Contain("pnpm-lock.yaml"));

        foreach (var component in detectedComponents)
        {
            component.Component.Type.Should().Be(ComponentType.Npm);
        }
    }

    // Test that renamed package is handled correctly, and that resolved version gets used (not specifier)
    [TestMethod]
    public async Task TestPnpmDetector_V6RenamedAsync()
    {
        var yamlFile = @"
lockfileVersion: '6.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
dependencies:
  renamed:
    specifier: npm:minimist@*
    version: /minimist@1.2.8
packages:
  /minimist@1.2.8:
    resolution: {integrity: sha512-2yyAR8qBkN3YuheJanUpWC5U3bb5osDywNB8RzDVlDwDHbocAJveqqj1u8+SVD7jkWT4yvsHCpWqqWqAxb0zCA==}
    dev: false
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var minimist = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Equals("minimist"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            minimist.Component.Id,
            parentComponent => parentComponent.Name == "minimist");
        ((NpmComponent)minimist.Component).Version.Should().BeEquivalentTo("1.2.8");

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.First().Should().Contain("pnpm-lock.yaml"));

        foreach (var component in detectedComponents)
        {
            component.Component.Type.Should().Be(ComponentType.Npm);
        }
    }

    [TestMethod]
    public async Task TestPnpmDetector_V6_BadLockVersion_EmptyAsync()
    {
        var yamlFile = @"
lockfileVersion: '5.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
dependencies:
  renamed:
    specifier: npm:minimist@*
    version: /minimist@1.2.8
packages:
  /minimist@1.2.8:
    resolution: {integrity: sha512-2yyAR8qBkN3YuheJanUpWC5U3bb5osDywNB8RzDVlDwDHbocAJveqqj1u8+SVD7jkWT4yvsHCpWqqWqAxb0zCA==}
    dev: false
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }
}
