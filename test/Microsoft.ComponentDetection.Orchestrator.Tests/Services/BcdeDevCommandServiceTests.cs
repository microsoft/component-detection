namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BcdeDevCommandServiceTests
{
    private readonly Mock<IBcdeScanExecutionService> scanExecutionServiceMock;
    private readonly Mock<ILogger<BcdeDevCommandService>> loggerMock;

    private readonly ScannedComponent[] scannedComponents;

    private BcdeDevCommandService serviceUnderTest;

    public BcdeDevCommandServiceTests()
    {
        this.scanExecutionServiceMock = new Mock<IBcdeScanExecutionService>();
        this.loggerMock = new Mock<ILogger<BcdeDevCommandService>>();
        this.serviceUnderTest = new BcdeDevCommandService(this.scanExecutionServiceMock.Object, this.loggerMock.Object);

        this.scannedComponents = new ScannedComponent[]
        {
            new ScannedComponent
            {
                Component = new NpmComponent("some-npm-component", "1.2.3"),
                IsDevelopmentDependency = false,
            },
        };

        var executeScanAsyncResult = new ScanResult
        {
            DetectorsInScan = new List<Detector>(),
            ComponentsFound = this.scannedComponents,
            ContainerDetailsMap = new Dictionary<int, ContainerDetails>(),
            ResultCode = ProcessingResultCode.Success,
            SourceDirectory = "D:\\test\\directory",
        };

        this.scanExecutionServiceMock.Setup(x => x.ExecuteScanAsync(It.IsAny<IDetectionArguments>()))
            .ReturnsAsync(executeScanAsyncResult);
    }

    [TestMethod]
    public async Task RunComponentDetectionAsync()
    {
        var args = new BcdeArguments();

        this.serviceUnderTest = new BcdeDevCommandService(
                this.scanExecutionServiceMock.Object,
                this.loggerMock.Object);

        var result = await this.serviceUnderTest.HandleAsync(args);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.SourceDirectory.Should().Be("D:\\test\\directory");
    }
}
