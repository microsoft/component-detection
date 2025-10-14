#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Gradle;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GradleComponentDetectorTests : BaseDetectorTest<GradleComponentDetector>
{
    private readonly Mock<IEnvironmentVariableService> envVarService;

    public GradleComponentDetectorTests()
    {
        this.envVarService = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.envVarService);
    }

    [TestMethod]
    public async Task TestGradleDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
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
        componentRecorder.GetDetectedComponents().Should().HaveCount(7);

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
        componentRecorder.GetDetectedComponents().Should().ContainSingle();

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
        componentRecorder.GetDetectedComponents().Should().HaveCount(5);

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

    [TestMethod]
    public async Task TestGradleDetector_DevDependenciesByLockfileNameAsync()
    {
        var regularLockfile =
            @"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE";

        var devLockfile1 = @"org.hamcrest:hamcrest-core:2.2
org.springframework:spring-core:5.0.5.RELEASE";

        var devLockfile2 = @"org.jacoco:org.jacoco.agent:0.8.8";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("settings-gradle.lockfile", devLockfile1)
            .WithFile("buildscript-gradle.lockfile", devLockfile2)
            .WithFile("gradle.lockfile", regularLockfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();
        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        var gradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith(Path.DirectorySeparatorChar + "gradle.lockfile"))];
        var settingsGradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith("settings-gradle.lockfile"))];
        var buildscriptGradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith("buildscript-gradle.lockfile"))];

        discoveredComponents.Should().HaveCount(4);

        // Dev dependency listed only in settings-gradle.lockfile
        var component = discoveredComponents[0];
        component.GroupId.Should().Be("org.hamcrest");
        component.ArtifactId.Should().Be("hamcrest-core");
        settingsGradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();

        // Dev dependency listed only in buildscript-gradle.lockfile
        component = discoveredComponents[1];
        component.GroupId.Should().Be("org.jacoco");
        component.ArtifactId.Should().Be("org.jacoco.agent");
        buildscriptGradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();

        // This should be purely a prod dependency, just a basic confidence test
        component = discoveredComponents[2];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-beans");
        gradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();

        // This is listed as both a prod and a dev dependency in different files
        component = discoveredComponents[3];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-core");
        gradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();
        settingsGradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestGradleDetector_DevDependenciesByDevLockfileEnvironmentAsync()
    {
        var regularLockfile =
            @"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE";

        var devLockfile1 = @"org.hamcrest:hamcrest-core:2.2
org.springframework:spring-core:5.0.5.RELEASE";

        var devLockfile2 = @"org.jacoco:org.jacoco.agent:0.8.8";

        this.envVarService.Setup(x => x.GetListEnvironmentVariable("CD_GRADLE_DEV_LOCKFILES", ",")).Returns(["dev1\\gradle.lockfile", "dev2\\gradle.lockfile"]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dev1\\gradle.lockfile", devLockfile1)
            .WithFile("dev2\\gradle.lockfile", devLockfile2)
            .WithFile("prod\\gradle.lockfile", regularLockfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();
        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        var gradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith("prod\\gradle.lockfile"))];
        var dev1GradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith("dev1\\gradle.lockfile"))];
        var dev2GradleLockfileGraph = dependencyGraphs[dependencyGraphs.Keys.First(k => k.EndsWith("dev2\\gradle.lockfile"))];

        discoveredComponents.Should().HaveCount(4);

        // Dev dependency listed only in dev1\gradle.lockfile
        var component = discoveredComponents[0];
        component.GroupId.Should().Be("org.hamcrest");
        component.ArtifactId.Should().Be("hamcrest-core");
        dev1GradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();

        // Dev dependency listed only in dev2\gradle.lockfile
        component = discoveredComponents[1];
        component.GroupId.Should().Be("org.jacoco");
        component.ArtifactId.Should().Be("org.jacoco.agent");
        dev2GradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();

        // This should be purely a prod dependency, just a basic confidence test
        component = discoveredComponents[2];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-beans");
        gradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();

        // This is listed as both a prod and a dev dependency in different files
        component = discoveredComponents[3];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-core");
        gradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();

        dev1GradleLockfileGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestGradleDetector_DevDependenciesByDevConfigurationEnvironmentAsync()
    {
        var lockfile =
                    @"org.springframework:spring-beans:5.0.5.RELEASE=assembleRelease
org.springframework:spring-core:5.0.5.RELEASE=assembleRelease,testDebugUnitTest
org.hamcrest:hamcrest-core:2.2=testReleaseUnitTest";

        this.envVarService.Setup(x => x.GetListEnvironmentVariable("CD_GRADLE_DEV_CONFIGURATIONS", ",")).Returns(["testDebugUnitTest", "testReleaseUnitTest"]);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("gradle.lockfile", lockfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        discoveredComponents.Should().HaveCount(3);

        var component = discoveredComponents[0];
        component.GroupId.Should().Be("org.hamcrest");
        component.ArtifactId.Should().Be("hamcrest-core");

        // Purely a dev dependency, only present in a test configuration
        dependencyGraph.IsDevelopmentDependency(component.Id).Should().BeTrue();

        component = discoveredComponents[1];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-beans");

        // Purely a prod dependency, only present in a prod configuration
        dependencyGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();

        component = discoveredComponents[2];
        component.GroupId.Should().Be("org.springframework");
        component.ArtifactId.Should().Be("spring-core");

        // Present in both dev and prod configurations, prod should win
        dependencyGraph.IsDevelopmentDependency(component.Id).Should().BeFalse();
    }
}
