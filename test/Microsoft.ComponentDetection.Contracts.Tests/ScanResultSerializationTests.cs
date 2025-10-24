#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ScanResultSerializationTests
{
    private ScanResult scanResultUnderTest;

    [TestInitialize]
    public void TestInitialize()
    {
        this.scanResultUnderTest = new ScanResult
        {
            ResultCode = ProcessingResultCode.PartialSuccess,
            ComponentsFound =
            [
                new ScannedComponent
                {
                    Component = new NpmComponent("SampleNpmComponent", "1.2.3"),
                    DetectorId = "NpmDetectorId",
                    IsDevelopmentDependency = true,
                    DependencyScope = DependencyScope.MavenCompile,
                    LocationsFoundAt =
                    [
                        "some/location",
                    ],
                    TopLevelReferrers =
                    [
                        new NpmComponent("RootNpmComponent", "4.5.6"),
                    ],
                },
            ],
            DetectorsInScan =
            [
                new Detector
                {
                    DetectorId = "NpmDetectorId",
                    IsExperimental = true,
                    SupportedComponentTypes =
                    [
                        ComponentType.Npm,
                    ],
                    Version = 2,
                },
            ],
            SourceDirectory = "D:\\test\\directory",
        };
    }

    [TestMethod]
    public void ScanResultSerialization_HappyPath()
    {
        var serializedResult = JsonConvert.SerializeObject(this.scanResultUnderTest);
        var actual = JsonConvert.DeserializeObject<ScanResult>(serializedResult);

        actual.ResultCode.Should().Be(ProcessingResultCode.PartialSuccess);
        actual.SourceDirectory.Should().Be("D:\\test\\directory");
        actual.ComponentsFound.Should().ContainSingle();
        var actualDetectedComponent = actual.ComponentsFound.First();
        actualDetectedComponent.DetectorId.Should().Be("NpmDetectorId");
        actualDetectedComponent.IsDevelopmentDependency.Should().Be(true);
        actualDetectedComponent.DependencyScope.Should().Be(DependencyScope.MavenCompile);
        actualDetectedComponent.LocationsFoundAt.Contains("some/location").Should().Be(true);

        var npmComponent = actualDetectedComponent.Component as NpmComponent;
        npmComponent.Should().NotBeNull();
        npmComponent.Name.Should().Be("SampleNpmComponent");
        npmComponent.Version.Should().Be("1.2.3");

        var rootNpmComponent = actualDetectedComponent.TopLevelReferrers.First() as NpmComponent;
        rootNpmComponent.Should().NotBeNull();
        rootNpmComponent.Name.Should().Be("RootNpmComponent");
        rootNpmComponent.Version.Should().Be("4.5.6");

        var actualDetector = actual.DetectorsInScan.First();
        actualDetector.DetectorId.Should().Be("NpmDetectorId");
        actualDetector.IsExperimental.Should().Be(true);
        actualDetector.Version.Should().Be(2);
        actualDetector.SupportedComponentTypes.Single().Should().Be(ComponentType.Npm);
    }

    [TestMethod]
    public void ScanResultSerialization_ExpectedJsonFormat()
    {
        var serializedResult = JsonConvert.SerializeObject(this.scanResultUnderTest);
        var json = JObject.Parse(serializedResult);

        json.Value<string>("resultCode").Should().Be("PartialSuccess");
        json.Value<string>("sourceDirectory").Should().Be("D:\\test\\directory");
        var foundComponent = json["componentsFound"].First();

        foundComponent.Value<string>("detectorId").Should().Be("NpmDetectorId");
        foundComponent.Value<bool>("isDevelopmentDependency").Should().Be(true);
        foundComponent.Value<string>("dependencyScope").Should().Be("MavenCompile");
        foundComponent["locationsFoundAt"].First().Value<string>().Should().Be("some/location");
        foundComponent["component"].Value<string>("type").Should().Be("Npm");
        foundComponent["component"].Value<string>("name").Should().Be("SampleNpmComponent");
        foundComponent["component"].Value<string>("version").Should().Be("1.2.3");

        var rootComponent = foundComponent["topLevelReferrers"].First();
        rootComponent.Value<string>("type").Should().Be("Npm");
        rootComponent.Value<string>("name").Should().Be("RootNpmComponent");
        rootComponent.Value<string>("version").Should().Be("4.5.6");

        var detector = json["detectorsInScan"].First();
        detector.Value<string>("detectorId").Should().Be("NpmDetectorId");
        detector.Value<int>("version").Should().Be(2);
        detector.Value<bool>("isExperimental").Should().Be(true);
        detector["supportedComponentTypes"].First().Value<string>().Should().Be("Npm");
    }
}
