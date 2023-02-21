namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Ivy;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class IvyDetectorTests : BaseDetectorTest<IvyDetector>
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;

    public IvyDetectorTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.DetectorTestUtility.AddServiceMock(this.commandLineMock);
        this.DetectorTestUtility.AddServiceMock(new Mock<ILogger<IvyDetector>>());
    }

    [TestMethod]
    public async Task IfAntIsNotAvailableThenExitDetectorGracefullyAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
            .ReturnsAsync(false);

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        Assert.AreEqual(componentRecorder.GetDetectedComponents().Count(), 0);
        Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task AntAvailableHappyPathAsync()
    {
        // Fake output from the IvyComponentDetectionAntTask
        var registerUsageContent = "{\"RegisterUsage\": [" +
                                   "{ \"gav\": { \"g\": \"d0g\", \"a\": \"d0a\", \"v\": \"0.0.0\"}, \"DevelopmentDependency\": false, \"resolved\": false},\n" +
                                   "{ \"gav\": { \"g\": \"d1g\", \"a\": \"d1a\", \"v\": \"1.1.1\"}, \"DevelopmentDependency\": true, \"resolved\": true},\n" +
                                   "{ \"gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}, \"DevelopmentDependency\": false, \"resolved\": true},\n" +
                                   "{ \"gav\": { \"g\": \"d3g\", \"a\": \"d3a\", \"v\": \"3.3.3\"}, \"DevelopmentDependency\": false, \"resolved\": true, \"parent_gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}},\n" +
                                   "]}";

        this.IvyHappyPath(content: registerUsageContent);

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents(); // IsDevelopmentDependency = true in componentRecorder but null in detectedComponents... why?
        Assert.AreEqual(3, detectedComponents.Count());
        Assert.AreEqual(ProcessingResultCode.Success, detectorResult.ResultCode);

        foreach (var detectedComponent in detectedComponents)
        {
            var dm = (MavenComponent)detectedComponent.Component;
            Assert.AreEqual(dm.ArtifactId.Replace('a', 'g'), dm.GroupId);
            Assert.AreEqual(dm.GroupId.Replace('g', 'a'), dm.ArtifactId);
            Assert.AreEqual(string.Format("{0}.{0}.{0}", dm.ArtifactId.Substring(1, 1)), dm.Version);
            Assert.AreEqual(ComponentType.Maven, dm.Type);

            // "Detector should not populate DetectedComponent.DevelopmentDependency" - see ComponentRecorder.cs.  Hence we get null not true (for d1g:d1a:1.1.1) or false here.
            Assert.IsNull(detectedComponent.DevelopmentDependency);

            // "Detector should not populate DetectedComponent.DependencyRoots!"
            Assert.IsNull(detectedComponent.DependencyRoots);
        }
    }

    [TestMethod]
    public async Task IvyDetector_FileObservableIsNotPresent_DetectionShouldNotFailAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
            .ReturnsAsync(true);

        Func<Task> action = async () => await this.DetectorTestUtility.ExecuteDetectorAsync();

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task IvyDependencyGraphAsync()
    {
        // Fake output from the IvyComponentDetectionAntTask
        var registerUsageContent = "{\"RegisterUsage\": [" +
                                   "{ \"gav\": { \"g\": \"d0g\", \"a\": \"d0a\", \"v\": \"0.0.0\"}, \"DevelopmentDependency\": false, \"resolved\": false},\n" +
                                   "{ \"gav\": { \"g\": \"d1g\", \"a\": \"d1a\", \"v\": \"1.1.1\"}, \"DevelopmentDependency\": true, \"resolved\": true},\n" +
                                   "{ \"gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}, \"DevelopmentDependency\": false, \"resolved\": true},\n" +
                                   "{ \"gav\": { \"g\": \"d3g\", \"a\": \"d3a\", \"v\": \"3.3.3\"}, \"DevelopmentDependency\": false, \"resolved\": true, \"parent_gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}},\n" +
                                   "]}";

        var d1Id = "d1g d1a 1.1.1 - Maven";
        var d2Id = "d2g d2a 2.2.2 - Maven";
        var d3Id = "d3g d3a 3.3.3 - Maven";

        this.IvyHappyPath(content: registerUsageContent);

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents(); // IsDevelopmentDependency = true in componentRecorder but null in detectedComponents... why?
        Assert.AreEqual(3, detectedComponents.Count());
        Assert.AreEqual(ProcessingResultCode.Success, detectorResult.ResultCode);

        // There is only one graph
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(d1Id).Should().HaveCount(0);
        dependencyGraph.IsComponentExplicitlyReferenced(d1Id).Should().BeTrue();
        dependencyGraph.IsDevelopmentDependency(d1Id).Should().BeTrue();

        dependencyGraph.GetDependenciesForComponent(d2Id).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(d2Id).Should().Contain(d3Id);
        dependencyGraph.IsComponentExplicitlyReferenced(d2Id).Should().BeTrue();
        dependencyGraph.IsDevelopmentDependency(d2Id).Should().BeFalse();

        dependencyGraph.GetDependenciesForComponent(d3Id).Should().HaveCount(0);
        dependencyGraph.IsComponentExplicitlyReferenced(d3Id).Should().BeFalse();
        dependencyGraph.IsDevelopmentDependency(d3Id).Should().BeFalse();
    }

    private void IvyHappyPath(string content)
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
            .ReturnsAsync(true);

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "ivy.xml"), "(dummy content)");
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "ivysettings.xml"), "(dummy content)");
        this.DetectorTestUtility
            .WithFile("ivy.xml", "(dummy content)", fileLocation: Path.Combine(Path.GetTempPath(), "ivy.xml"));

        this.commandLineMock.Setup(
            x => x.ExecuteCommandAsync(
                IvyDetector.PrimaryCommand,
                IvyDetector.AdditionalValidCommands,
                It.IsAny<string[]>())).Callback((string cmd, IEnumerable<string> cmd2, string[] parameters) =>
        {
            Assert.AreEqual(parameters[0], "-buildfile");
            var workingDir = parameters[1].Replace("build.xml", string.Empty);
            Directory.CreateDirectory(Path.Combine(workingDir, "target"));
            var jsonFileOutputPath = Path.Combine(workingDir, "target", "RegisterUsage.json");
            File.WriteAllText(jsonFileOutputPath, content);
            Assert.AreEqual(parameters[2], "resolve-dependencies");
        }).ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
        });
    }
}
