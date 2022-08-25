﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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
            this.commandLineMock = new Mock<ICommandLineInvocationService>();
            this.envVarService = new Mock<IEnvironmentVariableService>();

            var loggerMock = new Mock<ILogger>();

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(true);

            var detector = new GoComponentDetector
            {
                CommandLineInvocationService = this.commandLineMock.Object,
                Logger = loggerMock.Object,
                EnvVarService = this.envVarService.Object,
            };

            var tempPath = Path.GetTempPath();
            var detectionPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(detectionPath);

            this.scanRequest = new ScanRequest(new DirectoryInfo(detectionPath), (name, directoryName) => false, loggerMock.Object, null, null, new ComponentRecorder());

            this.detectorTestUtility = DetectorTestUtilityCreator.Create<GoComponentDetector>()
                                                            .WithScanRequest(this.scanRequest)
                                                            .WithDetector(detector);

            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
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
            var (scanResult, componentRecorder) = await this.detectorTestUtility
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

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("go.sum", goSum)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(6, detectedComponents.Count());
            var typedComponents = detectedComponents.Select(d => d.Component).ToList();
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

            var (scanResult, componentRecorder) = await this.detectorTestUtility
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

            var (scanResult, componentRecorder) = await this.detectorTestUtility
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
            var invalidGoMod =
@"     #/bin/sh
lorem ipsum
four score and seven bugs ago
$#26^#25%4";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
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

            var (scanResult, componentRecorder) = await this.detectorTestUtility
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
            var (scanResult, componentRecorder) = await this.detectorTestUtility
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
            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(false);

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoCommandThrows()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(() => throw new Exception("Some horrible error occured"));

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCommandFails()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            this.commandLineMock.Setup(x => x.ExecuteCommand("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 1,
                });

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCommandThrows()
        {
            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            this.commandLineMock.Setup(x => x.ExecuteCommand("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
                .ReturnsAsync(() => throw new Exception("Some horrible error occured"));

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfully();
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphHappyPath()
        {
            var buildDependencies = @"{
    ""Path"": ""some-package"",
    ""Version"": ""v1.2.3"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""test"",
    ""Version"": ""v2.0.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""other"",
    ""Version"": ""v1.2.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""a"",
    ""Version"": ""v1.5.0"",
    ""Time"": ""2020-05-19T17:02:07Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}";
            var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";

            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            this.commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "list", "-m", "-json", "all" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = buildDependencies,
                });

            this.commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = goGraph,
                });

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("go.mod", string.Empty)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());
            detectedComponents.Where(component => component.Component.Id == "other v1.0.0 - Go").Should().HaveCount(0);
            detectedComponents.Where(component => component.Component.Id == "other v1.2.0 - Go").Should().HaveCount(1);
            detectedComponents.Where(component => component.Component.Id == "some-package v1.2.3 - Go").Should().HaveCount(1);
            detectedComponents.Where(component => component.Component.Id == "test v2.0.0 - Go").Should().HaveCount(1);
            detectedComponents.Where(component => component.Component.Id == "a v1.5.0 - Go").Should().HaveCount(1);
        }

        [TestMethod]
        public async Task TestGoDetector_GoGraphCyclicDependencies()
        {
            var buildDependencies = @"{
    ""Path"": ""github.com/prometheus/common"",
    ""Version"": ""v0.32.1"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""github.com/prometheus/client_golang"",
    ""Version"": ""v1.11.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""github.com/prometheus/client_golang"",
    ""Version"": ""v1.12.1"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}";
            var goGraph = @"
github.com/prometheus/common@v0.32.1 github.com/prometheus/client_golang@v1.11.0
github.com/prometheus/client_golang@v1.12.1 github.com/prometheus/common@v0.32.1";
            this.commandLineMock.Setup(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
                .ReturnsAsync(true);

            this.commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "list", "-m", "-json", "all" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = buildDependencies,
                });

            this.commandLineMock.Setup(x => x.ExecuteCommand("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                    StdOut = goGraph,
                });

            this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("go.mod", string.Empty)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(3, detectedComponents.Count());
        }

        [TestMethod]
        public async Task TestGoDetector_GoCliRequiresEnvVarToRun()
        {
            await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfully();

            this.commandLineMock.Verify(x => x.CanCommandBeLocated("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()), Times.Never);
        }
    }
}
