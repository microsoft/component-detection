using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class GoComponentDetectorTests
    {
        private DetectorTestUtility<GoComponentDetector> detectorTestUtility;
        private Mock<ICommandLineInvocationService> commandLineMock;

        private Mock<IEnvironmentVariableService> envVarService;
        private ScanRequest scanRequest;

        [TestInitialize]
        public void TestInitialize()
        {
            commandLineMock = new Mock<ICommandLineInvocationService>();
            envVarService = new Mock<IEnvironmentVariableService>();

            var loggerMock = new Mock<ILogger>();

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(false);

            var detector = new GoComponentDetector
            {
                CommandLineInvocationService = commandLineMock.Object,
                Logger = loggerMock.Object,
                EnvVarService = envVarService.Object,
            };

            var tempPath = Path.GetTempPath();
            var detectionPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(detectionPath);

            scanRequest = new ScanRequest(new DirectoryInfo(detectionPath), (name, directoryName) => false, loggerMock.Object, null, null, new ComponentRecorder());

            detectorTestUtility = DetectorTestUtilityCreator.Create<GoComponentDetector>()
                                                            .WithScanRequest(scanRequest)
                                                            .WithDetector(detector);

            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(false);
        }

        [TestMethod]
        public async Task TestGoModDetectorWithValidFile_ReturnsSuccessfully()
        {
            var goMod =
@"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/dgrijalva/jwt-go v3.2.0+incompatible
)";
            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", goMod)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());

            var discoveredComponents = detectedComponents.ToArray();
            discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "github.com/dgrijalva/jwt-go v3.2.0+incompatible - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127 - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Count().Should().Be(1);
        }

        [TestMethod]
        public async Task TestGoSumDetectorWithValidFile_ReturnsSuccessfully()
        {
            var goSum =
@"
github.com/golang/mock v1.1.1/go.mod h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=
github.com/golang/mock v1.2.0/go.mod h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=
github.com/golang/protobuf v0.0.0-20161109072736-4bd1920723d7/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.2.0/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.3.1 h1:YF8+flBXS5eO826T4nzqPrxfhQThhXl0YzfuUPu4SBg=
github.com/golang/protobuf v1.3.1/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.3.2 h1:6nsPYzhq5kReh6QImI3k5qWzO4PEbvbIW2cwSfR/6xs=
github.com/golang/protobuf v1.3.2/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
)";

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.sum", goSum)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(6, detectedComponents.Count());
            List<TypedComponent> typedComponents = detectedComponents.Select(d => d.Component).ToList();
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/mock", "v1.1.1", "h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=")));
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/mock", "v1.2.0", "h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=")));
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/protobuf", "v0.0.0-20161109072736-4bd1920723d7", "h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=")));
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/protobuf", "v1.2.0", "h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=")));
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/protobuf", "v1.3.1", "h1:YF8+flBXS5eO826T4nzqPrxfhQThhXl0YzfuUPu4SBg=")));
            Assert.IsTrue(typedComponents.Contains(
                new GoComponent("github.com/golang/protobuf", "v1.3.2", "h1:6nsPYzhq5kReh6QImI3k5qWzO4PEbvbIW2cwSfR/6xs=")));
        }

        [TestMethod]
        public async Task TestGoModDetector_MultipleSpaces_ReturnsSuccessfully()
        {
            var goMod =
@"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go      v0.2.1
    github.com/kr/pretty    v0.1.0 // indirect
    gopkg.in/check.v1   v1.0.0-20180628173108-788fd7840127
    github.com/dgrijalva/jwt-go     v3.2.0+incompatible
)";

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", goMod)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());

            var discoveredComponents = detectedComponents.ToArray();
            discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "github.com/dgrijalva/jwt-go v3.2.0+incompatible - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127 - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Count().Should().Be(1);
        }

        [TestMethod]
        public async Task TestGoModDetector_ComponentsWithMultipleLocations_ReturnsSuccessfully()
        {
            var goMod1 =
@"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/Azure/go-autorest v10.15.2+incompatible
)";
            var goMod2 =
@"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/Azure/go-autorest v10.15.2+incompatible
)";

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", goMod1)
                                                    .WithFile("go.mod", goMod2, fileLocation: Path.Join(Path.GetTempPath(), "another-location", "go.mod"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(4, componentRecorder.GetDetectedComponents().Count());

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            Assert.IsTrue(dependencyGraphs.Keys.Count() == 2);

            var firstGraph = dependencyGraphs.Values.First();
            var secondGraph = dependencyGraphs.Values.Skip(1).First();

            firstGraph.GetComponents().Should().BeEquivalentTo(secondGraph.GetComponents());
        }

        [TestMethod]
        public async Task TestGoModDetectorInvalidFiles_DoesNotFail()
        {
            string invalidGoMod =
@"     #/bin/sh
lorem ipsum
four score and seven bugs ago
$#26^#25%4";

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", invalidGoMod)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestGoSumDetection_TwoEntriesForTheSameComponent_ReturnsSuccessfully()
        {
            var goSum =
@"
github.com/exponent-io/jsonpath v0.0.0-20151013193312-d6023ce2651d h1:105gxyaGwCFad8crR9dcMQWvV9Hvulu6hwUh4tWPJnM=
github.com/exponent-io/jsonpath v0.0.0-20151013193312-d6023ce2651d/go.mod h1:ZZMPRZwes7CROmyNKgQzC3XPs6L/G2EJLHddWejkmf4=
)";

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.sum", goSum)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
        }

        [TestMethod]
        public async Task TestGoModDetector_DetectorOnlyDetectInsideRequireSection()
        {
            var goMod =
@"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
)
replace (
	github.com/Azure/go-autorest => github.com/Azure/go-autorest v13.3.2+incompatible
	github.com/docker/distribution => github.com/docker/distribution v0.0.0-20191216044856-a8371794149d
)
";
            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", goMod)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(2, detectedComponents.Count());

            var discoveredComponents = detectedComponents.ToArray();
            discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Count().Should().Be(1);
            discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Count().Should().Be(1);
        }

        [TestMethod]
        public async Task TestGoDetector_GoCommandNotFound()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(false);

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            await TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoCommandThrows()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(() => throw new Exception("Some horrible error occured"));

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            await TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCommandFails()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            commandLineMock.Setup(x => x.ExecuteCommand("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 1,
                });

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            await TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCommandThrows()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            commandLineMock.Setup(x => x.ExecuteCommand("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
                .ReturnsAsync(() => throw new Exception("Some horrible error occured"));

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            await TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphHappyPath()
        {
            var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 test@v2.0.0\ntest@v2.0.0 a@v1.5.0";

            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = goGraph,
                });

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", string.Empty)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCyclicDependencies()
        {
            var goGraph = @"
github.com/prometheus/common@v0.32.1 github.com/prometheus/client_golang@v1.11.0
github.com/prometheus/client_golang@v1.12.1 github.com/prometheus/common@v0.32.1";
            commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = goGraph,
                });

            envVarService.Setup(x => x.DoesEnvironmentVariableExist("EnableGoCliScan")).Returns(true);

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("go.mod", string.Empty)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(3, detectedComponents.Count());
        }

        [TestMethod]
        public async Task TestGoDetector_GoCliRequiresEnvVarToRun()
        {
            await TestGoSumDetectorWithValidFile_ReturnsSuccessfully();

            commandLineMock.Verify(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()), Times.Never);
        }
    }
}
