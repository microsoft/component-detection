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

    [TestMethod]
    public void ScanResultSerialization_UnknownComponentType_IsSkipped()
    {
        // Simulate a ScanResult JSON with an unknown component type
        // This tests forward compatibility when new component types are added
        var scanResultJson = """
            {
                "resultCode": "Success",
                "sourceDirectory": "D:\\test\\directory",
                "componentsFound": [
                    {
                        "detectorId": "NpmDetectorId",
                        "component": {
                            "type": "Npm",
                            "name": "KnownNpmComponent",
                            "version": "1.0.0"
                        },
                        "locationsFoundAt": ["some/location"]
                    },
                    {
                        "detectorId": "FutureDetectorId",
                        "component": {
                            "type": "FutureComponentType",
                            "name": "UnknownComponent",
                            "version": "2.0.0"
                        },
                        "locationsFoundAt": ["another/location"]
                    }
                ],
                "detectorsInScan": []
            }
            """;

        var result = JsonSerializer.Deserialize<ScanResult>(scanResultJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.SourceDirectory.Should().Be("D:\\test\\directory");

        // Both components are in the array, but the unknown one has a null Component
        result.ComponentsFound.Should().HaveCount(2);

        var knownComponent = result.ComponentsFound.First();
        knownComponent.Component.Should().NotBeNull();
        knownComponent.Component.Should().BeOfType<NpmComponent>();
        ((NpmComponent)knownComponent.Component).Name.Should().Be("KnownNpmComponent");

        var unknownComponent = result.ComponentsFound.Last();
        unknownComponent.Component.Should().BeNull();
        unknownComponent.DetectorId.Should().Be("FutureDetectorId");
    }

    [TestMethod]
    public void ScanResultSerialization_UnknownTopLevelReferrer_IsNull()
    {
        // Test that unknown component types in TopLevelReferrers are handled gracefully
        var scanResultJson = """
            {
                "resultCode": "Success",
                "sourceDirectory": "D:\\test\\directory",
                "componentsFound": [
                    {
                        "detectorId": "NpmDetectorId",
                        "component": {
                            "type": "Npm",
                            "name": "ChildComponent",
                            "version": "1.0.0"
                        },
                        "locationsFoundAt": ["some/location"],
                        "topLevelReferrers": [
                            {
                                "type": "Npm",
                                "name": "KnownParent",
                                "version": "2.0.0"
                            },
                            {
                                "type": "FutureComponentType",
                                "name": "UnknownParent",
                                "version": "3.0.0"
                            }
                        ]
                    }
                ],
                "detectorsInScan": []
            }
            """;

        var result = JsonSerializer.Deserialize<ScanResult>(scanResultJson);

        var component = result.ComponentsFound.First();
        component.Component.Should().NotBeNull();

        // TopLevelReferrers should contain both entries, with the unknown one being null
        component.TopLevelReferrers.Should().HaveCount(2);
        var referrers = component.TopLevelReferrers.ToList();

        referrers[0].Should().NotBeNull();
        referrers[0].Should().BeOfType<NpmComponent>();
        ((NpmComponent)referrers[0]).Name.Should().Be("KnownParent");

        referrers[1].Should().BeNull();
    }

    [TestMethod]
    public void ScanResultSerialization_AllUnknownComponents_StillDeserializes()
    {
        // Edge case: all components are unknown types
        var scanResultJson = """
            {
                "resultCode": "Success",
                "sourceDirectory": "D:\\test\\directory",
                "componentsFound": [
                    {
                        "detectorId": "FutureDetector1",
                        "component": {
                            "type": "FutureType1",
                            "name": "Component1",
                            "version": "1.0.0"
                        },
                        "locationsFoundAt": ["location1"]
                    },
                    {
                        "detectorId": "FutureDetector2",
                        "component": {
                            "type": "FutureType2",
                            "name": "Component2",
                            "version": "2.0.0"
                        },
                        "locationsFoundAt": ["location2"]
                    }
                ],
                "detectorsInScan": []
            }
            """;

        var result = JsonSerializer.Deserialize<ScanResult>(scanResultJson);

        result.Should().NotBeNull();
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.ComponentsFound.Should().HaveCount(2);

        // All components should be null, but the ScannedComponent wrapper should still exist
        foreach (var scannedComponent in result.ComponentsFound)
        {
            scannedComponent.Component.Should().BeNull();
            scannedComponent.DetectorId.Should().NotBeNullOrEmpty();
        }
    }
}
