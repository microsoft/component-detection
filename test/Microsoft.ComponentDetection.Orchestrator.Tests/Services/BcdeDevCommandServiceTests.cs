using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class BcdeDevCommandServiceTests
    {
        private Mock<IBcdeScanExecutionService> scanExecutionServiceMock;

        private BcdeDevCommandService serviceUnderTest;

        private ScannedComponent[] scannedComponents;

        [TestInitialize]
        public void InitializeTest()
        {
            this.scanExecutionServiceMock = new Mock<IBcdeScanExecutionService>();
            this.serviceUnderTest = new BcdeDevCommandService();

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
        public async Task RunComponentDetection()
        {
            var args = new BcdeArguments();

            this.serviceUnderTest = new BcdeDevCommandService
            {
                BcdeScanExecutionService = this.scanExecutionServiceMock.Object,
            };

            var result = await this.serviceUnderTest.Handle(args);
            result.ResultCode.Should().Be(ProcessingResultCode.Success);
            result.SourceDirectory.Should().Be("D:\\test\\directory");
        }
    }
}
