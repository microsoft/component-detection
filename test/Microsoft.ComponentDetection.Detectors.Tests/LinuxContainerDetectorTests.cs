#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LinuxContainerDetectorTests
{
    private const string NodeLatestImage = "node:latest";
    private const string NodeLatestDigest = "2a22e4a1a550";
    private const string BashPackageId = "Ubuntu 20.04 bash 5.0-6ubuntu1 - Linux";

    private static readonly IEnumerable<LayerMappedLinuxComponents> LinuxComponents =
    [
        new LayerMappedLinuxComponents
        {
            DockerLayer = new DockerLayer(),
            LinuxComponents = [new LinuxComponent("Ubuntu", "20.04", "bash", "5.0-6ubuntu1")],
        },
    ];

    private readonly Mock<IDockerService> mockDockerService;
    private readonly Mock<ILogger> mockLogger;
    private readonly Mock<ILogger<LinuxContainerDetector>> mockLinuxContainerDetectorLogger;
    private readonly Mock<ILinuxScanner> mockSyftLinuxScanner;

    public LinuxContainerDetectorTests()
    {
        this.mockDockerService = new Mock<IDockerService>();
        this.mockDockerService.Setup(service => service.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockDockerService.Setup(service => service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockDockerService.Setup(service => service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerDetails { Id = 1, ImageId = NodeLatestDigest, Layers = [] });

        this.mockLogger = new Mock<ILogger>();
        this.mockLinuxContainerDetectorLogger = new Mock<ILogger<LinuxContainerDetector>>();

        this.mockSyftLinuxScanner = new Mock<ILinuxScanner>();
        this.mockSyftLinuxScanner.Setup(scanner => scanner.ScanLinuxAsync(It.IsAny<string>(), It.IsAny<IEnumerable<DockerLayer>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LinuxComponents);
    }

    [TestMethod]
    public async Task TestLinuxContainerDetectorAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, null, [NodeLatestImage], componentRecorder);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents.Should().OnlyContain(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id));
        componentRecorder.GetDetectedComponents().Select(detectedComponent => detectedComponent.Component.Id)
            .Should().BeEquivalentTo(detectedComponents.Select(detectedComponent => detectedComponent.Component.Id));
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_CantRunLinuxContainersAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, null, [NodeLatestImage], componentRecorder);

        this.mockDockerService.Setup(service => service.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
        scanResult.ContainerDetails.Should().BeEmpty();
        this.mockLinuxContainerDetectorLogger.Verify(logger => logger.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_TestNullAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, null, null, componentRecorder);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
        scanResult.ContainerDetails.Should().BeEmpty();
        this.mockLinuxContainerDetectorLogger.Verify(logger => logger.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_VerifyLowerCaseAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, null, ["UPPERCASE"], componentRecorder);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents.Should().OnlyContain(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id));
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_SameImagePassedMultipleTimesAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, null, [NodeLatestImage, NodeLatestDigest], componentRecorder);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        detectedComponents.Should().OnlyContain(dc => dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id));
        this.mockSyftLinuxScanner.Verify(scanner => scanner.ScanLinuxAsync(It.IsAny<string>(), It.IsAny<IEnumerable<DockerLayer>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_TimeoutParameterSpecifiedAsync()
    {
        var detectorArgs = new Dictionary<string, string> { { "Linux.ScanningTimeoutSec", "2" } };
        var scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), (_, __) => false, this.mockLogger.Object, detectorArgs, [NodeLatestImage], new ComponentRecorder());

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object);

        Func<Task> action = async () => await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);
        await action.Should().NotThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_HandlesScratchBaseAsync()
    {
        // Setup docker service to throw an exception on scratch
        // then specify that the base image is scratch, to test this
        // is coped with.
        this.mockDockerService.Setup(service => service.TryPullImageAsync("scratch", It.IsAny<CancellationToken>()))
            .Throws(new IOException());
        this.mockDockerService.Setup(service => service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))

            // Specify BaseImageRef = scratch to verify that cope
            .ReturnsAsync(new ContainerDetails { Id = 1, ImageId = NodeLatestDigest, Layers = [], BaseImageRef = "scratch" });
        await this.TestLinuxContainerDetectorAsync();
    }
}
