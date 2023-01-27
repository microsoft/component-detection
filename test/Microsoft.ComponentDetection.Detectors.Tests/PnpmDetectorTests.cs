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
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PnpmDetectorTests
{
    private DetectorTestUtility<PnpmComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.detectorTestUtility = DetectorTestUtilityCreator.Create<PnpmComponentDetector>()
            .WithScanRequest(new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder));
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(5, detectedComponents.Count());

        var queryString = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("query-string"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            queryString.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌");

        Assert.AreEqual("4.3.4", ((NpmComponent)queryString.Component).Version);
        Assert.IsTrue(componentRecorder.GetEffectiveDevDependencyValue(queryString.Component.Id).GetValueOrDefault(false));

        var objectAssign = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("object-assign"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            objectAssign.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        Assert.AreEqual("4.1.1", ((NpmComponent)objectAssign.Component).Version);
        Assert.IsTrue(componentRecorder.GetEffectiveDevDependencyValue(objectAssign.Component.Id).GetValueOrDefault(false));

        var strictUriEncode = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("strict-uri-encode"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            strictUriEncode.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        Assert.AreEqual("1.1.0", ((NpmComponent)strictUriEncode.Component).Version);
        Assert.IsFalse(componentRecorder.GetEffectiveDevDependencyValue(strictUriEncode.Component.Id).GetValueOrDefault(true));

        var babelHelperCompilation = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("helper-compilation-targets"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            babelHelperCompilation.Component.Id,
            parentComponent => parentComponent.Name == "@babel/helper-compilation-targets" && parentComponent.Version == "7.10.4");
        Assert.IsFalse(componentRecorder.GetEffectiveDevDependencyValue(babelHelperCompilation.Component.Id).GetValueOrDefault(true));

        var test = detectedComponents.Single(component => ((NpmComponent)component.Component).Name.Contains("test"));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            test.Component.Id,
            parentComponent => parentComponent.Name == "query-string-🙌" && parentComponent.Version == "4.3.4");
        Assert.IsTrue(componentRecorder.GetEffectiveDevDependencyValue(test.Component.Id).GetValueOrDefault(false));

        componentRecorder.ForAllComponents(grouping => Assert.IsTrue(grouping.AllFileLocations.First().Contains("shrinkwrap1.yaml")));

        foreach (var component in detectedComponents)
        {
            Assert.AreEqual(component.Component.Type, ComponentType.Npm);
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .WithFile("shrinkwrap2.yaml", yamlFile2)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(3, detectedComponents.Count());
        var strictUriEncodeComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("strict-uri-encode"));

        Assert.IsNotNull(strictUriEncodeComponent);

        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            strictUriEncodeComponent.Component.Id,
            parentComponent => parentComponent.Name == "some-other-root",
            parentComponent => parentComponent.Name == "query-string");

        componentRecorder.ForOneComponent(strictUriEncodeComponent.Component.Id, grouping => Assert.AreEqual(2, grouping.AllFileLocations.Count()));
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(2, detectedComponents.Count());
        var msItemsViewComponent = detectedComponents.Select(x => new { Component = x.Component as NpmComponent, DetectedComponent = x }).FirstOrDefault(x => x.Component.Name.Contains("@ms/items-view"));

        Assert.IsNotNull(msItemsViewComponent);
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile1)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(4, componentRecorder.GetDetectedComponents().Count());

        var queryStringComponentId = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4").Component.Id;
        var objectAssignComponentId = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/object-assign/4.1.1").Component.Id;
        var strictUriComponentId = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/strict-uri-encode/1.1.0").Component.Id;
        var testComponentId = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/test/1.0.0").Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var queryStringDependencies = dependencyGraph.GetDependenciesForComponent(queryStringComponentId);
        Assert.AreEqual(2, queryStringDependencies.Count());
        Assert.IsTrue(queryStringDependencies.Contains(objectAssignComponentId));
        Assert.IsTrue(queryStringDependencies.Contains(testComponentId));

        var objectAssignDependencies = dependencyGraph.GetDependenciesForComponent(objectAssignComponentId);
        Assert.AreEqual(1, objectAssignDependencies.Count());
        Assert.IsTrue(objectAssignDependencies.Contains(strictUriComponentId));

        var stringUriDependencies = dependencyGraph.GetDependenciesForComponent(strictUriComponentId);
        Assert.AreEqual(0, stringUriDependencies.Count());

        var testDependencies = dependencyGraph.GetDependenciesForComponent(testComponentId);
        Assert.AreEqual(0, testDependencies.Count());
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

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("shrinkwrap1.yaml", yamlFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2, "Components that comes from a file (file:* or link:*) should be ignored.");

        var queryStringComponentId = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4").Component.Id;
        var nthcheck = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/nth-check/2.0.0").Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        var queryStringDependencies = dependencyGraph.GetDependenciesForComponent(queryStringComponentId);
        queryStringDependencies.Should().HaveCount(1);
        queryStringDependencies.Should().Contain(nthcheck);

        var nthCheckDependencies = dependencyGraph.GetDependenciesForComponent(nthcheck);
        nthCheckDependencies.Should().HaveCount(0);
    }
}
