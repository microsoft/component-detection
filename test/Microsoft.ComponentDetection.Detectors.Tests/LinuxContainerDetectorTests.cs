using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class LinuxContainerDetectorTests
    {
        private const string NodeLatestImage = "node:latest";
        private const string NodeLatestDigest = "2a22e4a1a550";
        private const string BashPackageId = "Ubuntu 20.04 bash 5.0-6ubuntu1 - Linux";

        private static readonly IEnumerable<LayerMappedLinuxComponents> LinuxComponents = new List<LayerMappedLinuxComponents>
            {
                new LayerMappedLinuxComponents {
                    DockerLayer = new DockerLayer { },
                    LinuxComponents = new List<LinuxComponent> { new LinuxComponent("Ubuntu", "20.04", "bash", "5.0-6ubuntu1") },
                },
            };

        private Mock<IDockerService> mockDockerService;
        private Mock<ILogger> mockLogger;
        private Mock<ILinuxScanner> mockSyftLinuxScanner;

        [TestInitialize]
        public void TestInitialize()
        {
            mockDockerService = new Mock<IDockerService>();
            mockDockerService.Setup(service => service.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockDockerService.Setup(service => service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockDockerService.Setup(service => service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerDetails { Id = 1, ImageId = NodeLatestDigest, Layers = Enumerable.Empty<DockerLayer>() });

            mockLogger = new Mock<ILogger>();

            mockSyftLinuxScanner = new Mock<ILinuxScanner>();
            mockSyftLinuxScanner.Setup(scanner => scanner.ScanLinuxAsync(It.IsAny<string>(), It.IsAny<IEnumerable<DockerLayer>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LinuxComponents);
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector()
        {
            var componentRecorder = new ComponentRecorder();

            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object,
                null, new List<string> { NodeLatestImage }, componentRecorder);

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            detectedComponents.Should().ContainSingle();
            detectedComponents.First().Component.Id.Should().Be(BashPackageId);
            scanResult.ContainerDetails.Should().ContainSingle();
            detectedComponents.All(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)).Should().BeTrue();
            componentRecorder.GetDetectedComponents().Select(detectedComponent => detectedComponent.Component.Id)
                .Should().BeEquivalentTo(detectedComponents.Select(detectedComponent => detectedComponent.Component.Id));
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_CantRunLinuxContainers()
        {
            var componentRecorder = new ComponentRecorder();

            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object, null,
                new List<string> { NodeLatestImage }, componentRecorder);

            mockDockerService.Setup(service => service.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            detectedComponents.Should().HaveCount(0);
            scanResult.ContainerDetails.Should().HaveCount(0);
            mockLogger.Verify(logger => logger.LogInfo(It.IsAny<string>()));
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_TestNull()
        {
            var componentRecorder = new ComponentRecorder();

            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object, null,
                null, componentRecorder);

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            var detectedComponents = componentRecorder.GetDetectedComponents();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            detectedComponents.Should().HaveCount(0);
            scanResult.ContainerDetails.Should().HaveCount(0);
            mockLogger.Verify(logger => logger.LogInfo(It.IsAny<string>()));
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_VerifyLowerCase()
        {
            var componentRecorder = new ComponentRecorder();

            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object, null,
                new List<string> { "UPPERCASE" }, componentRecorder);

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            detectedComponents.Should().ContainSingle();
            detectedComponents.First().Component.Id.Should().Be(BashPackageId);
            scanResult.ContainerDetails.Should().HaveCount(1);
            detectedComponents.All(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)).Should().BeTrue();
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_SameImagePassedMultipleTimes()
        {
            var componentRecorder = new ComponentRecorder();

            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object, null,
                new List<string> { NodeLatestImage, NodeLatestDigest }, componentRecorder);

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().HaveCount(1);
            detectedComponents.Should().HaveCount(1);
            detectedComponents.First().Component.Id.Should().Be(BashPackageId);
            detectedComponents.All(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)).Should().BeTrue();
            mockSyftLinuxScanner.Verify(scanner => scanner.ScanLinuxAsync(It.IsAny<string>(), It.IsAny<IEnumerable<DockerLayer>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_TimeoutParameterSpecified()
        {
            var detectorArgs = new Dictionary<string, string> { { "Linux.ScanningTimeoutSec", "2" } };
            var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, mockLogger.Object,
                detectorArgs, new List<string> { NodeLatestImage }, new ComponentRecorder());

            var linuxContainerDetector = new LinuxContainerDetector
            {
                LinuxScanner = mockSyftLinuxScanner.Object,
                Logger = mockLogger.Object,
                DockerService = mockDockerService.Object,
            };

            Func<Task> action = async () => await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);
            await action.Should().NotThrowAsync<OperationCanceledException>();
        }

        [TestMethod]
        public async Task TestLinuxContainerDetector_HandlesScratchBase() {
            // Setup docker service to throw an exception on scratch
            // then specify that the base image is scratch, to test this
            // is coped with.
            mockDockerService.Setup(service => service.TryPullImageAsync("scratch", It.IsAny<CancellationToken>()))
                .Throws(new IOException());
            mockDockerService.Setup(service => service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                // Specify BaseImageRef = scratch to verify that cope 
                .ReturnsAsync(new ContainerDetails { Id = 1, ImageId = NodeLatestDigest, Layers = Enumerable.Empty<DockerLayer>() , BaseImageRef = "scratch"});
            await TestLinuxContainerDetector();
        }
    }
}
