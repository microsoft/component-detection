#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Sbt;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class SbtDetectorTests : BaseDetectorTest<SbtComponentDetector>
{
    private readonly Mock<ISbtCommandService> sbtCommandServiceMock;

    public SbtDetectorTests()
    {
        this.sbtCommandServiceMock = new Mock<ISbtCommandService>();
        this.DetectorTestUtility.AddServiceMock(this.sbtCommandServiceMock);
    }

    [TestMethod]
    public async Task IfSbtIsNotAvailableThenExitDetectorGracefullyAsync()
    {
        this.sbtCommandServiceMock.Setup(x => x.SbtCLIExistsAsync())
            .ReturnsAsync(false);

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        componentRecorder.GetDetectedComponents().Should().BeEmpty();
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task SbtAvailableHappyPathAsync()
    {
        const string componentString = "org.typelevel:cats-core_3:2.9.0";

        this.SbtCliHappyPath(content: componentString);
        this.sbtCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) => pr.SingleFileComponentRecorder.RegisterUsage(
                new DetectedComponent(new MavenComponent("org.typelevel", "cats-core_3", "2.9.0"))));

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.GroupId.Should().Be("org.typelevel");
        mavenComponent.ArtifactId.Should().Be("cats-core_3");
        mavenComponent.Version.Should().Be("2.9.0");
        mavenComponent.Type.Should().Be(ComponentType.Maven);
    }

    [TestMethod]
    public async Task SbtCli_FileObservableIsNotPresent_DetectionShouldNotFailAsync()
    {
        this.sbtCommandServiceMock.Setup(x => x.SbtCLIExistsAsync())
            .ReturnsAsync(true);

        Func<Task> action = async () => await this.DetectorTestUtility.ExecuteDetectorAsync();

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task SbtDetector_DetectsScalaDependenciesAsync()
    {
        const string scalaTestComponent = "org.scalatest:scalatest_3:3.2.15";
        const string catsComponent = "org.typelevel:cats-core_3:2.9.0";

        var content = $@"default:my-scala-project:1.0.0{Environment.NewLine}\- {catsComponent}{Environment.NewLine}\- {scalaTestComponent}";

        this.SbtCliHappyPath(content);
        this.sbtCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("default", "my-scala-project", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.typelevel", "cats-core_3", "2.9.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.scalatest", "scalatest_3", "3.2.15")),
                    isExplicitReferencedDependency: true);
            });

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        detectedComponents.Should().Contain(x => (x.Component as MavenComponent).ArtifactId == "cats-core_3");
        detectedComponents.Should().Contain(x => (x.Component as MavenComponent).ArtifactId == "scalatest_3");
    }

    private void SbtCliHappyPath(string content, string fileName = "build.sbt")
    {
        this.sbtCommandServiceMock.Setup(x => x.SbtCLIExistsAsync())
            .ReturnsAsync(true);

        this.sbtCommandServiceMock.Setup(x => x.BcdeSbtDependencyFileName).Returns("bcde.sbtdeps");

        this.DetectorTestUtility
            .WithFile(fileName, string.Empty)
            .WithFile("bcde.sbtdeps", content, ["bcde.sbtdeps"]);
    }
}
