namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MvnCliDetectorTests : BaseDetectorTest<MvnCliComponentDetector>
{
    private readonly Mock<IMavenCommandService> mavenCommandServiceMock;

    public MvnCliDetectorTests()
    {
        this.mavenCommandServiceMock = new Mock<IMavenCommandService>();
        this.detectorTestUtility.AddServiceMock(this.mavenCommandServiceMock);
    }

    [TestMethod]
    public async Task IfMavenIsNotAvailableThenExitDetectorGracefullyAsync()
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExists())
            .ReturnsAsync(false);

        var (detectorResult, componentRecorder) = await this.detectorTestUtility
            .ExecuteDetectorAsync();

        Assert.AreEqual(componentRecorder.GetDetectedComponents().Count(), 0);
        Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task MavenAvailableHappyPathAsync()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        this.MvnCliHappyPath(content: componentString);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) => pr.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT"))));
        var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(detectedComponents.Count(), 1);
        Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        var splitComponent = componentString.Split(':');
        Assert.AreEqual(splitComponent[0], mavenComponent.GroupId);
        Assert.AreEqual(splitComponent[1], mavenComponent.ArtifactId);
        Assert.AreEqual(splitComponent[3], mavenComponent.Version);
        Assert.AreEqual(ComponentType.Maven, mavenComponent.Type);
    }

    [TestMethod]
    public async Task MavenCli_FileObservableIsNotPresent_DetectionShouldNotFailAsync()
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExists())
            .ReturnsAsync(true);

        Func<Task> action = async () => await this.detectorTestUtility.ExecuteDetectorAsync();

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task MavenRootsAsync()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        const string childComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

        var content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine} \- {childComponentString}";

        this.MvnCliHappyPath(content);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("com.bcde.test", "top-levelt", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-child", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven");
            });

        var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(detectedComponents.Count(), 3);
        Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);

        var splitComponent = componentString.Split(':');
        var splitChildComponent = childComponentString.Split(':');

        var mavenComponent = detectedComponents.FirstOrDefault(x => (x.Component as MavenComponent).ArtifactId == splitChildComponent[1]);
        Assert.IsNotNull(mavenComponent);

        componentRecorder.AssertAllExplicitlyReferencedComponents<MavenComponent>(
            mavenComponent.Component.Id,
            parentComponent => parentComponent.ArtifactId == splitComponent[1]);
    }

    [TestMethod]
    public async Task MavenDependencyGraphAsync()
    {
        const string explicitReferencedComponent = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        const string intermediateParentComponent = "org.apache.maven:maven-compat-parent:jar:3.6.1-SNAPSHOT";

        const string leafComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

        var content = $@"com.bcde.test:top-level:jar:1.0.0
\- {explicitReferencedComponent}
    \- {intermediateParentComponent}
        \-{leafComponentString}";

        const string explicitReferencedComponentId = "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven";
        const string intermediateParentComponentId = "org.apache.maven maven-compat-parent 3.6.1-SNAPSHOT - Maven";
        const string leafComponentId = "org.apache.maven maven-compat-child 3.6.1-SNAPSHOT - Maven";

        this.MvnCliHappyPath(content);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("com.bcde.test", "top-levelt", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-parent", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven");
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-child", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat-parent 3.6.1-SNAPSHOT - Maven");
            });

        var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        componentRecorder.GetDetectedComponents().Should().HaveCount(4);
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // There is only one graph
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(explicitReferencedComponentId).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(explicitReferencedComponentId).Should().Contain(intermediateParentComponentId);
        dependencyGraph.IsComponentExplicitlyReferenced(explicitReferencedComponentId).Should().BeTrue();

        dependencyGraph.GetDependenciesForComponent(intermediateParentComponentId).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(intermediateParentComponentId).Should().Contain(leafComponentId);
        dependencyGraph.IsComponentExplicitlyReferenced(intermediateParentComponentId).Should().BeFalse();

        dependencyGraph.GetDependenciesForComponent(leafComponentId).Should().HaveCount(0);
        dependencyGraph.IsComponentExplicitlyReferenced(leafComponentId).Should().BeFalse();
    }

    protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
    {
        result.Should<T>().BeEquivalentTo(expected);
        return true;
    }

    private void MvnCliHappyPath(string content)
    {
        const string bcdeMvnFileName = "bcde.mvndeps";

        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns(bcdeMvnFileName);
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExists())
            .ReturnsAsync(true);
        this.detectorTestUtility.WithFile("pom.xml", content)
            .WithFile("pom.xml", content, searchPatterns: new[] { bcdeMvnFileName });
    }
}
