using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class MvnCliDetectorTests
    {
        private IMavenCommandService mavenCommandService;
        private Mock<ICommandLineInvocationService> commandLineMock;
        private DetectorTestUtility<MvnCliComponentDetector> detectorTestUtility;
        private ScanRequest scanRequest;

        [TestInitialize]
        public void InitializeTests()
        {
            this.commandLineMock = new Mock<ICommandLineInvocationService>();
            this.mavenCommandService = new MavenCommandService
            {
                CommandLineInvocationService = this.commandLineMock.Object,
                ParserService = new MavenStyleDependencyGraphParserService(),
            };

            var loggerMock = new Mock<ILogger>();

            var detector = new MvnCliComponentDetector
            {
                MavenCommandService = this.mavenCommandService,
                Logger = loggerMock.Object,
            };

            var tempPath = Path.GetTempPath();
            var detectionPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(detectionPath);

            this.scanRequest = new ScanRequest(new DirectoryInfo(detectionPath), (name, directoryName) => false, loggerMock.Object, null, null, new ComponentRecorder());

            this.detectorTestUtility = DetectorTestUtilityCreator.Create<MvnCliComponentDetector>()
                                                            .WithScanRequest(this.scanRequest)
                                                            .WithDetector(detector);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.scanRequest.SourceDirectory.Delete();
        }

        [TestMethod]
        public async Task IfMavenIsNotAvailableThenExitDetectorGracefully()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                MavenCommandService.MvnVersionArgument)).ReturnsAsync(false);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

            Assert.AreEqual(componentRecorder.GetDetectedComponents().Count(), 0);
            Assert.AreEqual(detectorResult.ResultCode, ProcessingResultCode.Success);
        }

        [TestMethod]
        public async Task MavenAvailableHappyPath()
        {
            const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

            this.MvnCliHappyPath(content: componentString);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

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
        public async Task MavenCli_FileObservableIsNotPresent_DetectionShouldNotFail()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                MavenCommandService.MvnVersionArgument)).ReturnsAsync(true);

            Func<Task> action = async () => await this.detectorTestUtility.ExecuteDetector();

            await action.Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task MavenRoots()
        {
            const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
            const string childComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

            string content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine} \- {childComponentString}";

            this.MvnCliHappyPath(content);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility
                                                   .ExecuteDetector();

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
        public async Task MavenDependencyGraph()
        {
            const string explicitReferencedComponent = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

            const string intermediateParentComponent = "org.apache.maven:maven-compat-parent:jar:3.6.1-SNAPSHOT";

            const string leafComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

            string content = $@"com.bcde.test:top-level:jar:1.0.0
\- {explicitReferencedComponent}
    \- {intermediateParentComponent}
        \-{leafComponentString}";

            const string explicitReferencedComponentId = "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven";
            const string intermediateParentComponentId = "org.apache.maven maven-compat-parent 3.6.1-SNAPSHOT - Maven";
            const string leafComponentId = "org.apache.maven maven-compat-child 3.6.1-SNAPSHOT - Maven";

            this.MvnCliHappyPath(content);

            var (detectorResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

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

        private void MvnCliHappyPath(string content)
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated(MavenCommandService.PrimaryCommand, MavenCommandService.AdditionalValidCommands, MavenCommandService.MvnVersionArgument)).ReturnsAsync(true);

            var expectedPomLocation = this.scanRequest.SourceDirectory.FullName;

            var bcdeMvnFileName = "bcde.mvndeps";
            this.detectorTestUtility.WithFile("pom.xml", content, fileLocation: expectedPomLocation)
                                .WithFile("pom.xml", content, searchPatterns: new[] { bcdeMvnFileName }, fileLocation: Path.Combine(expectedPomLocation, "pom.xml"));

            var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{expectedPomLocation}" };

            this.commandLineMock.Setup(x => x.ExecuteCommand(
                                                        MavenCommandService.PrimaryCommand,
                                                        MavenCommandService.AdditionalValidCommands,
                                                        It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                });
        }

        protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
        {
            result.Should<T>().BeEquivalentTo(expected);
            return true;
        }
    }
}
