namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Gradle;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GradleComponentDetectorTests : BaseDetectorTest<GradleComponentDetector>
{
    [TestMethod]
    public async Task TestGradleDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(0);
    }

    [TestMethod]
    public async Task TestGradleDetectorWithValidFile_DetectsComponentsSuccessfullyAsync()
    {
        var validFileOne =
            @"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE
org.springframework:spring-jcl:5.0.5.RELEASE";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", validFileOne)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();

        discoveredComponents.Should().HaveCount(3);

        discoveredComponents[0].GroupId.Should().Be("org.springframework");
        discoveredComponents[0].ArtifactId.Should().Be("spring-beans");
        discoveredComponents[0].Version.Should().Be("5.0.5.RELEASE");

        discoveredComponents[1].GroupId.Should().Be("org.springframework");
        discoveredComponents[1].ArtifactId.Should().Be("spring-core");
        discoveredComponents[1].Version.Should().Be("5.0.5.RELEASE");

        discoveredComponents[2].GroupId.Should().Be("org.springframework");
        discoveredComponents[2].ArtifactId.Should().Be("spring-jcl");
        discoveredComponents[2].Version.Should().Be("5.0.5.RELEASE");
    }

    [TestMethod]
    public async Task TestGradleDetectorWithValidSingleLockfilePerProject_DetectsComponentsSuccessfullyAsync()
    {
        var validFileOne =
            @"org.springframework:spring-beans:5.0.5.RELEASE=lintClassPath
org.springframework:spring-core:5.0.5.RELEASE=debugCompile,releaseCompile
org.springframework:spring-jcl:5.0.5.RELEASE=lintClassPath,debugCompile,releaseCompile";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", validFileOne)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var discoveredComponents = detectedComponents.Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();

        discoveredComponents.Should().HaveCount(3);

        discoveredComponents[0].GroupId.Should().Be("org.springframework");
        discoveredComponents[0].ArtifactId.Should().Be("spring-beans");
        discoveredComponents[0].Version.Should().Be("5.0.5.RELEASE");

        discoveredComponents[1].GroupId.Should().Be("org.springframework");
        discoveredComponents[1].ArtifactId.Should().Be("spring-core");
        discoveredComponents[1].Version.Should().Be("5.0.5.RELEASE");

        discoveredComponents[2].GroupId.Should().Be("org.springframework");
        discoveredComponents[2].ArtifactId.Should().Be("spring-jcl");
        discoveredComponents[2].Version.Should().Be("5.0.5.RELEASE");
    }

    [TestMethod]
    public async Task TestGradleDetectorWithValidFiles_ReturnsSuccessfullyAsync()
    {
        var validFileOne =
            @"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE
org.springframework:spring-jcl:5.0.5.RELEASE";

        var validFileTwo =
            @"com.fasterxml.jackson.core:jackson-annotations:2.8.0
com.fasterxml.jackson.core:jackson-core:2.8.10
com.fasterxml.jackson.core:jackson-databind:2.8.11.3
org.msgpack:msgpack-core:0.8.16
org.springframework:spring-jcl:5.0.5.RELEASE";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", validFileOne)
            .WithFile("gradle2.lockfile", validFileTwo)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(7);

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        dependencyGraphs.Keys.Should().HaveCount(2);

        var graph1 = dependencyGraphs.Values.Single(dependencyGraph => dependencyGraph.GetComponents().Count() == 3);
        var graph2 = dependencyGraphs.Values.Single(dependencyGraph => dependencyGraph.GetComponents().Count() == 5);

        var expectedComponents = new List<string>
        {
            // Graph 1
            "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
            "org.springframework spring-beans 5.0.5.RELEASE - Maven",
            "org.springframework spring-core 5.0.5.RELEASE - Maven",

            // Graph 2
            "org.msgpack msgpack-core 0.8.16 - Maven",
            "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
            "com.fasterxml.jackson.core jackson-core 2.8.10 - Maven",
            "com.fasterxml.jackson.core jackson-annotations 2.8.0 - Maven",
            "com.fasterxml.jackson.core jackson-databind 2.8.11.3 - Maven",
        };

        foreach (var componentId in expectedComponents)
        {
            var component = componentRecorder.GetComponent(componentId);
            component.Should().NotBeNull();
        }
    }

    [TestMethod]
    public async Task TestGradleDetector_SameComponentDifferentLocations_DifferentLocationsAreSavedAsync()
    {
        var validFileOne =
            @"org.springframework:spring-beans:5.0.5.RELEASE";

        var validFileTwo =
            "org.springframework:spring-beans:5.0.5.RELEASE";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", validFileOne)
            .WithFile("gradle2.lockfile", validFileTwo)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(1);

        componentRecorder.ForOneComponent(componentRecorder.GetDetectedComponents().First().Component.Id, x =>
        {
            x.AllFileLocations.Should().HaveCount(2);
        });

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        dependencyGraphs.Keys.Should().HaveCount(2);

        var graph1 = dependencyGraphs.Values.First();
        var graph2 = dependencyGraphs.Values.Skip(1).First();

        graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents());
    }

    [TestMethod]
    public async Task TestGradleDetectorWithInvalidAndValidFiles_ReturnsSuccessfullyAsync()
    {
        var validFileTwo =
            @"com.fasterxml.jackson.core:jackson-annotations:2.8.0
com.fasterxml.jackson.core:jackson-core:2.8.10
com.fasterxml.jackson.core:jackson-databind:2.8.11.3
org.msgpack:msgpack-core:0.8.16
org.springframework:spring-jcl:5.0.5.RELEASE";

        var invalidFileOne =
            @"     #/bin/sh
lorem ipsum
four score and seven bugs ago
$#26^#25%4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", invalidFileOne)
            .WithFile("gradle2.lockfile", validFileTwo)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(5);

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        dependencyGraphs.Keys.Should().ContainSingle();

        var graph2 = dependencyGraphs.Values.Single();

        var expectedComponents = new List<string>
        {
            // Graph 2
            "org.msgpack msgpack-core 0.8.16 - Maven",
            "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
            "com.fasterxml.jackson.core jackson-core 2.8.10 - Maven",
            "com.fasterxml.jackson.core jackson-annotations 2.8.0 - Maven",
            "com.fasterxml.jackson.core jackson-databind 2.8.11.3 - Maven",
        };

        foreach (var componentId in expectedComponents)
        {
            var component = componentRecorder.GetComponent(componentId);
            component.Should().NotBeNull();
        }
    }
}
