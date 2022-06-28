using System.Collections.Generic;
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
            scanExecutionServiceMock = new Mock<IBcdeScanExecutionService>();
            serviceUnderTest = new BcdeDevCommandService();

            scannedComponents = new ScannedComponent[]
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
                ComponentsFound = scannedComponents,
                ContainerDetailsMap = new Dictionary<int, ContainerDetails>(),
                ResultCode = ProcessingResultCode.Success,
                SourceDirectory = "D:\\test\\directory",
            };

            scanExecutionServiceMock.Setup(x => x.ExecuteScanAsync(It.IsAny<IDetectionArguments>()))
                .ReturnsAsync(executeScanAsyncResult);
        }

        [TestMethod]
        public void RunComponentDetection()
        {
            var args = new BcdeArguments();

            serviceUnderTest = new BcdeDevCommandService
            {
                BcdeScanExecutionService = scanExecutionServiceMock.Object,
            };

            var result = serviceUnderTest.Handle(args);
            result.Result.ResultCode.Should().Be(ProcessingResultCode.Success);
            result.Result.SourceDirectory.Should().Be("D:\\test\\directory");
        }
    }
}
