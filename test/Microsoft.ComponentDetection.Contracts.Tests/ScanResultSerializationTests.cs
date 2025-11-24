#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        var serializedResult = JsonSerializer.Serialize(this.scanResultUnderTest);
        var actual = JsonSerializer.Deserialize<ScanResult>(serializedResult);

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
        var serializedResult = JsonSerializer.Serialize(this.scanResultUnderTest);
        var json = JsonNode.Parse(serializedResult);

        json["resultCode"].GetValue<string>().Should().Be("PartialSuccess");
        json["sourceDirectory"].GetValue<string>().Should().Be("D:\\test\\directory");
        var foundComponent = json["componentsFound"][0];

        foundComponent["detectorId"].GetValue<string>().Should().Be("NpmDetectorId");
        foundComponent["isDevelopmentDependency"].GetValue<bool>().Should().Be(true);
        foundComponent["dependencyScope"].GetValue<string>().Should().Be("MavenCompile");
        foundComponent["locationsFoundAt"][0].GetValue<string>().Should().Be("some/location");
        foundComponent["component"]["type"].GetValue<string>().Should().Be("Npm");
        foundComponent["component"]["name"].GetValue<string>().Should().Be("SampleNpmComponent");
        foundComponent["component"]["version"].GetValue<string>().Should().Be("1.2.3");

        var rootComponent = foundComponent["topLevelReferrers"][0];
        rootComponent["type"].GetValue<string>().Should().Be("Npm");
        rootComponent["name"].GetValue<string>().Should().Be("RootNpmComponent");
        rootComponent["version"].GetValue<string>().Should().Be("4.5.6");

        var detector = json["detectorsInScan"][0];
        detector["detectorId"].GetValue<string>().Should().Be("NpmDetectorId");
        detector["version"].GetValue<int>().Should().Be(2);
        detector["isExperimental"].GetValue<bool>().Should().Be(true);
        detector["supportedComponentTypes"][0].GetValue<string>().Should().Be("Npm");
    }
}
