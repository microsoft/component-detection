namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MvnPomComponentDetectorTest : BaseDetectorTest<MavenPomComponentDetector>
{
    private readonly Mock<IMavenFileParserService> mavenFileParserServiceMock;

    public MvnPomComponentDetectorTest()
    {
        this.mavenFileParserServiceMock = new Mock<IMavenFileParserService>();
        this.DetectorTestUtility.AddServiceMock(this.mavenFileParserServiceMock);
    }

    [TestMethod]
    public async Task MavenRootsAsync()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        const string childComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";
        var content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine} \- {childComponentString}";
        this.DetectorTestUtility.WithFile("pom.xml", content)
                   .WithFile("pom.xml", content, searchPatterns: new[] { "pom.xml" });

        this.mavenFileParserServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
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

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

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
}
