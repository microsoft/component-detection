#nullable disable
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
public class PnpmDetectorTests : BaseDetectorTest<PnpmComponentDetectorFactory>
{
    public PnpmDetectorTests()
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
    public async Task TestPnpmDetector_SingleFileLocatesExpectedInputAsync()
    {
        var yamlFile = @"
dependencies:
  'query-string': 4.3.4,
  '@babel/helper-compilation-targets': 7.10.4_@babel+core@7.10.5

packages:
  /query-string-🙌/4.3.4:
    dependencies:
      object-assign: 4.1.1
      strict-uri-encode: 1.1.0
      test: 1.0.0
    dev: true
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-u7aTucqRXCMlFbIosaArYJBD2+s=
  /object-assign/4.1.1:
    dev: true
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-IQmtx5ZYh8/AXLvUQsrIv7s2CGM=
  /strict-uri-encode/1.1.0:
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-J5siXfHVgrH1TmWt3UNS4Y+qBxM=
  /test/1.0.0:
    dev: true
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-A5siXfHVgrH1TmWt3UNS4Y+qBxM=
  /@babel/helper-compilation-targets/7.10.4_@babel+core@7.10.5:
    dev: false
registry: 'https://test/registry'
shrinkwrapMinorVersion: 7
shrinkwrapVersion: 3";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(5);

        var queryString = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("query-string"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            queryString.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌");

        ((NpmComponent)queryString.Component).Version.Should().Be("4.3.4");
        componentRecorder.GetEffectiveDevDependencyValue(queryString.Component.Id).GetValueOrDefault(false).Should().BeTrue();

        var objectAssign = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("object-assign"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            objectAssign.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        ((NpmComponent)objectAssign.Component).Version.Should().Be("4.1.1");
        componentRecorder.GetEffectiveDevDependencyValue(objectAssign.Component.Id).GetValueOrDefault(false).Should().BeTrue();

        var strictUriEncode = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("strict-uri-encode"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            strictUriEncode.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        ((NpmComponent)strictUriEncode.Component).Version.Should().Be("1.1.0");
        componentRecorder.GetEffectiveDevDependencyValue(strictUriEncode.Component.Id).GetValueOrDefault(true).Should().BeFalse();

        var babelHelperCompilation = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("helper-compilation-targets"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            babelHelperCompilation.Component.Id,
            parentComponent => parentComponent.Name == "@babel/helper-compilation-targets" && parentComponent.Version == "7.10.4");
        componentRecorder.GetEffectiveDevDependencyValue(babelHelperCompilation.Component.Id).GetValueOrDefault(true).Should().BeFalse();

        var test = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("test"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            test.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        componentRecorder.GetEffectiveDevDependencyValue(test.Component.Id).GetValueOrDefault(false).Should().BeTrue();

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.First().Should().Contain("shrinkwrap1.yaml"));

        foreach (var component in detectedComponents)
        {
            ComponentType.Npm.Should().Be(component.Component.Type);
        }
    }

    [TestMethod]
    public async Task TestPnpmDetector_SameComponentMergesRootsAndLocationsAcrossMultipleFilesAsync()
    {
        var yamlFile1 = @"
dependencies:
  'query-string': 4.3.4
packages:
  /query-string/4.3.4:
    dependencies:
      strict-uri-encode: 1.1.0
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-u7aTucqRXCMlFbIosaArYJBD2+s=
  /strict-uri-encode/1.1.0:
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-J5siXfHVgrH1TmWt3UNS4Y+qBxM=
registry: 'https://test/registry'
shrinkwrapMinorVersion: 7
shrinkwrapVersion: 3";

        var yamlFile2 = @"
dependencies:
  'some-other-root': 1.2.3
packages:
  /some-other-root/1.2.3:
    dependencies:
      strict-uri-encode: 1.1.0
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-u7aTucqRXCMlFbIosaArYJBD2+s=
  /strict-uri-encode/1.1.0:
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-J5siXfHVgrH1TmWt3UNS4Y+qBxM=
registry: 'https://test/registry'
shrinkwrapMinorVersion: 7
shrinkwrapVersion: 3";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .WithFile("shrinkwrap2.yaml", yamlFile2)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        var strictUriEncodeComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("strict-uri-encode"));

        strictUriEncodeComponent.Should().NotBeNull();

        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            strictUriEncodeComponent.Component.Id,
            parentComponent => parentComponent.Name == "some-other-root",
            parentComponent => parentComponent.Name == "query-string");

        componentRecorder.ForOneComponent(strictUriEncodeComponent.Component.Id, grouping => grouping.AllFileLocations.Should().HaveCount(2));
    }

    [TestMethod]
    public async Task TestPnpmDetector_SpecialDependencyVersionStringDoesntBlowUsUpAsync()
    {
        var yamlFile1 = @"
dependencies:
  'query-string': 4.3.4
packages:
  /query-string/4.3.4:
    dependencies:
      '@ms/items-view': /@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-u7aTucqRXCMlFbIosaArYJBD2+s=
  /@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2:
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-J5siXfHVgrH1TmWt3UNS4Y+qBxM=
registry: 'https://test/registry'
shrinkwrapMinorVersion: 7
shrinkwrapVersion: 3";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);
        var msItemsViewComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("@ms/items-view"));

        msItemsViewComponent.Should().NotBeNull();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            msItemsViewComponent.Component.Id,
            parentComponent => parentComponent.Name == "query-string");
    }

    [TestMethod]
    public async Task TestPnpmDetector_DetectorRecognizeDevDependenciesValuesAsync()
    {
        var yamlFile1 = @"
                dependencies:
                  'query-string': 4.3.4,
                  'strict-uri-encode': 1.1.0
                packages:
                  /query-string/4.3.4:
                    dev: false
                  /strict-uri-encode/1.1.0:
                    dev: true";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var noDevDependencyComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("query-string"));
        var devDependencyComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("strict-uri-encode"));

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(devDependencyComponent.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPnpmDetector_DetectorRecognizeDevDependenciesValues_InWeirdCasesAsync()
    {
        var yamlFile1 = @"
                dependencies:
                  'query-string': 4.3.4,
                  'strict-uri-encode': 1.1.0
                packages:
                  /query-string/4.3.4:
                    dependencies:
                      solo-non-dev-dep: 0.1.2
                      shared-non-dev-dep: 0.1.2
                    dev: false
                  /strict-uri-encode/1.1.0:
                    dependencies:
                      solo-dev-dep: 0.1.2
                      shared-non-dev-dep: 0.1.2
                    dev: true";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        componentRecorder.GetEffectiveDevDependencyValue("solo-non-dev-dep 0.1.2 - Npm").Value.Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("solo-dev-dep 0.1.2 - Npm").Value.Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("shared-non-dev-dep 0.1.2 - Npm").Value.Should().BeFalse();
    }

    [TestMethod]
    public async Task TestPnpmDetector_HandlesMalformedYamlAsync()
    {
        // This is a clearly malformed Yaml. We expect parsing it to "succeed" but find no components
        var yamlFile1 = @"dependencies";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPnpmDetector_DependencyGraphIsCreatedAsync()
    {
        var yamlFile = @"
dependencies:
  'query-string': 4.3.4,

packages:
  /query-string/4.3.4:
    dependencies:
      object-assign: 4.1.1
      test: 1.0.0
    dev: false
  /object-assign/4.1.1:
    dependencies:
      strict-uri-encode: 1.1.0
    dev: false
  /strict-uri-encode/1.1.0:
    dev: false
  /test/1.0.0:
    dev: true";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV5>();

        var queryStringComponentId = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4").Component.Id;
        var objectAssignComponentId = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/object-assign/4.1.1").Component.Id;
        var strictUriComponentId = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/strict-uri-encode/1.1.0").Component.Id;
        var testComponentId = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/test/1.0.0").Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var queryStringDependencies = dependencyGraph.GetDependenciesForComponent(queryStringComponentId);
        queryStringDependencies.Should().HaveCount(2);
        queryStringDependencies.Should().Contain(objectAssignComponentId);
        queryStringDependencies.Should().Contain(testComponentId);

        var objectAssignDependencies = dependencyGraph.GetDependenciesForComponent(objectAssignComponentId);
        objectAssignDependencies.Should().ContainSingle();
        objectAssignDependencies.Should().Contain(strictUriComponentId);

        var stringUriDependencies = dependencyGraph.GetDependenciesForComponent(strictUriComponentId);
        stringUriDependencies.Should().BeEmpty();

        var testDependencies = dependencyGraph.GetDependenciesForComponent(testComponentId);
        testDependencies.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPnpmDetector_DependenciesRefeToLocalPaths_DependenciesAreIgnoredAsync()
    {
        var yamlFile = @"
dependencies:
  'query-string': 4.3.4,
  '@rush-temp/file-annotation-bar': file:projects/file-annotation-bar.tgz_node-sass@4.14.1

packages:
  file:projects/file-annotation-bar.tgz_node-sass@4.14.1:
     resolution: {integrity: sha1-G7T22scAcvwxPoyc0UF7UHTAoSU=}
  /query-string/4.3.4:
    dependencies:
      '@learningclient/common': link:../common
      nth-check: 2.0.0
  /nth-check/2.0.0:
    resolution: {integrity: sha1-G7T22scAcvwxPoyc0UF7UHTAoSU=} ";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2, "Components that comes from a file (file:* or link:*) should be ignored.");

        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV5>();

        var queryStringComponentId = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4").Component.Id;
        var nthcheck = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/nth-check/2.0.0").Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var queryStringDependencies = dependencyGraph.GetDependenciesForComponent(queryStringComponentId);
        queryStringDependencies.Should().ContainSingle();
        queryStringDependencies.Should().Contain(nthcheck);

        var nthCheckDependencies = dependencyGraph.GetDependenciesForComponent(nthcheck);
        nthCheckDependencies.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPnpmDetector_BadLockVersion_EmptyAsync()
    {
        var yamlFile = @"
lockfileVersion: '4.0'
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

    [TestMethod]
    public async Task TestPnpmDetector_V5_GoodLockVersion_ParsedDependenciesAsync()
    {
        var yamlFile = @"
lockfileVersion: '5.0'
dependencies:
  'query-string': 4.3.4,
  'strict-uri-encode': 1.1.0
packages:
  /query-string/4.3.4:
    dev: false
  /strict-uri-encode/1.1.0:
    dev: true";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var noDevDependencyComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("query-string"));
        var devDependencyComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("strict-uri-encode"));

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(devDependencyComponent.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPnpmDetector_V6_SuccessAsync()
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
    public async Task TestPnpmDetector_V6_WorkspaceAsync()
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
    public async Task TestPnpmDetector_V6_RenamedAsync()
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

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_ParsesDependencies()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      sampleDependency:
        specifier: ^1.1.1
        version: 1.1.1
    devDependencies:
      sampleDevDependency:
        specifier: ^2.2.2
        version: 2.2.2
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleDevDependency@2.2.2:
    resolution: {integrity: sha512-FQN4MRfuJeHf7cBbBMJFXhKSDq+2kAArBlmRBvcvFE5BB1HZKXtSFASDhdlz9zOYwxh8lDdnvmMOe/+5cdoEdg==}
    engines: {node: '>= 0.8'}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}

snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
  sampleDevDependency@2.2.2: {}
  sampleIndirectDependency@3.3.3: {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDevDependency" && x.Component.Version == "2.2.2");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var devDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDevDependency");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(devDependencyComponent.Component.Id).Should().BeTrue();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_ParsesAliasedDependency()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      aliasedSample:
        specifier: npm:sampleDependency@1.1.1
        version: sampleDependency@1.1.1
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}

snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
  sampleIndirectDependency@3.3.3: {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_SkipsFileAndLinkDependencies()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      sampleDependency:
        specifier: ^1.1.1
        version: 1.1.1
      SampleFileDependency:
        specifier: file://../sampleFile
        version: link:../
      SampleLinkDependency:
        specifier: workspace:*
        version: link:SampleLinkDependency
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleIndirectDependency2@2.2.2:
    resolution: {integrity: sha512-FQN4MRfuJeHf7cBbBMJFXhKSDq+2kAArBlmRBvcvFE5BB1HZKXtSFASDhdlz9zOYwxh8lDdnvmMOe/+5cdoEdg==}
    engines: {node: '>= 0.8'}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}

snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
      sampleIndirectDependency2: 2.2.2
      'file://../sampleFile':  'link:../\\'
  sampleIndirectDependency2@2.2.2: {}
  sampleIndirectDependency@3.3.3: {}
  'file://../sampleFile@link:../': {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency2" && x.Component.Version == "2.2.2");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var indirectDependencyComponent2 = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency2");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent2.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent2.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_FileAndLinkDependenciesAreNotRegistered()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      sampleDependency:
        specifier: ^1.1.1
        version: 1.1.1
      sampleFileDependency:
        specifier: file:../test
        version: file:../test
      SampleLinkDependency:
        specifier: workspace:*
        version: link:SampleLinkDependency
    devDependencies:
      sampleDevDependency:
        specifier: ^4.4.4
        version: 4.4.4
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleIndirectDependency2@2.2.2:
    resolution: {integrity: sha512-FQN4MRfuJeHf7cBbBMJFXhKSDq+2kAArBlmRBvcvFE5BB1HZKXtSFASDhdlz9zOYwxh8lDdnvmMOe/+5cdoEdg==}
    engines: {node: '>= 0.8'}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}
  sampleDevDependency@4.4.4:
    resolution: {integrity: sha512-n4ZT37wG78iz03xPRKJrHTdZbe3IicyucEtdRsV5yglwc3GyUfbAfpSeD0FJ41NbUNSt5wbhqfp1fS+BgnvDFQ==}
  sampleIndirectDevDependency@5.5.5:
    resolution: {integrity: sha512-R3pbpkcIqv2Pm3dUwgjclDRVmWpTJW2DcMzcIhEXEx1oh/CEMObMm3KLmRJOdvhM7o4uQBnwr8pzRK2sJWIqfg==}
    engines: {node: '>=0.4.0'}
  
snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
      sampleIndirectDependency2: 2.2.2
      'file://../sampleFile':  'link:../\\'
  sampleIndirectDependency2@2.2.2: {}
  sampleIndirectDependency@3.3.3: {}
  sampleFileDependency@file:../test': {}
  sampleDevDependency@4.4.4:
    dependencies:
      sampleIndirectDevDependency: 5.5.5
  sampleIndirectDevDependency@5.5.5: {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(5);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency2" && x.Component.Version == "2.2.2");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDevDependency" && x.Component.Version == "4.4.4");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDevDependency" && x.Component.Version == "5.5.5");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var indirectDependencyComponent2 = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency2");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");
        var devDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDevDependency");
        var indirectDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDevDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent2.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(devDependencyComponent.Component.Id).Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDevDependencyComponent.Component.Id).Should().BeTrue();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent2.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDevDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDevDependency");

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        var allExplicitlyReferenced = dependencyGraph.GetAllExplicitlyReferencedComponents();
        allExplicitlyReferenced.Should().HaveCount(2);
        allExplicitlyReferenced.Should().Contain(noDevDependencyComponent.Component.Id);
        allExplicitlyReferenced.Should().Contain(devDependencyComponent.Component.Id);
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_HttpDependenciesAreNotRegistered()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      sampleDependency:
        specifier: ^1.1.1
        version: 1.1.1
      sampleHttpDependency:
        specifier: https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e
        version: sample@https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e
      SampleLinkDependency:
        specifier: workspace:*
        version: link:SampleLinkDependency
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleIndirectDependency2@2.2.2:
    resolution: {integrity: sha512-FQN4MRfuJeHf7cBbBMJFXhKSDq+2kAArBlmRBvcvFE5BB1HZKXtSFASDhdlz9zOYwxh8lDdnvmMOe/+5cdoEdg==}
    engines: {node: '>= 0.8'}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}

snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
      sampleIndirectDependency2: 2.2.2
      'file://../sampleFile':  'link:../\\'
  sampleIndirectDependency2@2.2.2: {}
  sampleIndirectDependency@3.3.3: {}
  sampleHttpDependency@https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e': {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency2" && x.Component.Version == "2.2.2");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var indirectDependencyComponent2 = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency2");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent2.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent2.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_HttpsDependenciesAreNotRegistered()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      sampleDependency:
        specifier: ^1.1.1
        version: 1.1.1
      '@sample_dependency/sampleHttpDependency':
        specifier: https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e
        version: sample@https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e
      SampleLinkDependency:
        specifier: workspace:*
        version: link:SampleLinkDependency
packages:
  sampleDependency@1.1.1:
    resolution: {integrity: sha512-zRpUiDwd/xk6ADqPMATG8vc9VPrkck7T07OIx0gnjmJAnHnTVXNQG3vfvWNuiZIkwu9KrKdA1iJKfsfTVxE6NA==}
  sampleIndirectDependency2@2.2.2:
    resolution: {integrity: sha512-FQN4MRfuJeHf7cBbBMJFXhKSDq+2kAArBlmRBvcvFE5BB1HZKXtSFASDhdlz9zOYwxh8lDdnvmMOe/+5cdoEdg==}
    engines: {node: '>= 0.8'}
  sampleIndirectDependency@3.3.3:
    resolution: {integrity: sha512-ZySD7Nf91aLB0RxL4KGrKHBXl7Eds1DAmEdcoVawXnLD7SDhpNgtuII2aAkg7a7QS41jxPSZ17p4VdGnMHk3MQ==}
    engines: {node: '>=0.4.0'}
  sample@https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e:
    resolution: {tarball: https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e}
    version: 1.3.3

snapshots:
  sampleDependency@1.1.1:
    dependencies:
      sampleIndirectDependency: 3.3.3
      sampleIndirectDependency2: 2.2.2
  sampleIndirectDependency2@2.2.2: {}
  sampleIndirectDependency@3.3.3: {}
  sample@https://samplePackage/tar.gz/32f550d3b3bdb1b781aabe100683311cd982c98e': {}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        var npmComponents = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x });
        npmComponents.Should().Contain(x => x.Component.Name == "sampleDependency" && x.Component.Version == "1.1.1");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency2" && x.Component.Version == "2.2.2");
        npmComponents.Should().Contain(x => x.Component.Name == "sampleIndirectDependency" && x.Component.Version == "3.3.3");

        var noDevDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleDependency");
        var indirectDependencyComponent2 = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency2");
        var indirectDependencyComponent = npmComponents.First(x => x.Component.Name == "sampleIndirectDependency");

        componentRecorder.GetEffectiveDevDependencyValue(noDevDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent2.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(indirectDependencyComponent.Component.Id).Should().BeFalse();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            indirectDependencyComponent2.Component.Id,
            parentComponent => parentComponent.Name == "sampleDependency");
    }

    [TestMethod]
    public async Task TestPnpmDetector_V9_GoodLockVersion_MissingSnapshotsSuccess()
    {
        var yamlFile = @"
lockfileVersion: '9.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
importers:
  .:
    dependencies:
      SampleLinkDependency:
        specifier: workspace:*
        version: link:SampleLinkDependency
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pnpm-lock.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }
}
