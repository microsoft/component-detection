using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Ivy;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class IvyDetectorTests
    {
        private Mock<ICommandLineInvocationService> commandLineMock;
        private DetectorTestUtility<IvyDetector> detectorTestUtility;
        private ScanRequest scanRequest;

        [TestInitialize]
        public void InitializeTests()
        {
            this.commandLineMock = new Mock<ICommandLineInvocationService>();
            var loggerMock = new Mock<ILogger>();

            var detector = new IvyDetector
            {
                CommandLineInvocationService = this.commandLineMock.Object,
                Logger = loggerMock.Object,
            };

            var tempPath = Path.GetTempPath();
            var detectionPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(detectionPath);

            this.scanRequest = new ScanRequest(new DirectoryInfo(detectionPath), (name, directoryName) => false, loggerMock.Object, null, null, new ComponentRecorder());

            this.detectorTestUtility = DetectorTestUtilityCreator.Create<IvyDetector>()
                                                            .WithScanRequest(this.scanRequest)
                                                            .WithDetector(detector);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.scanRequest.SourceDirectory.Delete(recursive: true);
        }

        [TestMethod]
        public async Task IfAntIsNotAvailableThenExitDetectorGracefully()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
                .ReturnsAsync(false);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

            Assert.AreEqual(componentRecorder.GetDetectedComponents().Count(), 0);
            Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);
        }

        [TestMethod]
        public async Task AntAvailableHappyPath()
        {
            // Fake output from the IvyComponentDetectionAntTask
            var registerUsageContent = "{\"RegisterUsage\": [" +
                "{ \"gav\": { \"g\": \"d0g\", \"a\": \"d0a\", \"v\": \"0.0.0\"}, \"DevelopmentDependency\": false, \"resolved\": false},\n" +
                "{ \"gav\": { \"g\": \"d1g\", \"a\": \"d1a\", \"v\": \"1.1.1\"}, \"DevelopmentDependency\": true, \"resolved\": true},\n" +
                "{ \"gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}, \"DevelopmentDependency\": false, \"resolved\": true},\n" +
                "{ \"gav\": { \"g\": \"d3g\", \"a\": \"d3a\", \"v\": \"3.3.3\"}, \"DevelopmentDependency\": false, \"resolved\": true, \"parent_gav\": { \"g\": \"d2g\", \"a\": \"d2a\", \"v\": \"2.2.2\"}},\n" +
                "]}";

            this.IvyHappyPath(content: registerUsageContent);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

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
        public async Task IvyDetector_FileObservableIsNotPresent_DetectionShouldNotFail()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
                            .ReturnsAsync(true);

            Func<Task> action = async () => await this.detectorTestUtility.ExecuteDetector();

            await action.Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task IvyDependencyGraph()
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

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

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

        protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
        {
            result.Should<T>().BeEquivalentTo(expected);
            return true;
        }

        private void IvyHappyPath(string content)
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(IvyDetector.PrimaryCommand, IvyDetector.AdditionalValidCommands, IvyDetector.AntVersionArgument))
                            .ReturnsAsync(true);

            var expectedIvyXmlLocation = this.scanRequest.SourceDirectory.FullName;

            File.WriteAllText(Path.Combine(expectedIvyXmlLocation, "ivy.xml"), "(dummy content)");
            File.WriteAllText(Path.Combine(expectedIvyXmlLocation, "ivysettings.xml"), "(dummy content)");
            this.detectorTestUtility
                .WithFile("ivy.xml", "(dummy content)", fileLocation: Path.Combine(expectedIvyXmlLocation, "ivy.xml"));

            this.commandLineMock.Setup(
                x => x.ExecuteCommand(
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
}
