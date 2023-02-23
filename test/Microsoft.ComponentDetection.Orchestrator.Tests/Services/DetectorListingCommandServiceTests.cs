namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectorListingCommandServiceTests
{
    private Mock<ILogger<DetectorListingCommandService>> loggerMock;
    private Mock<IEnumerable<IComponentDetector>> detectorsMock;
    private Mock<IComponentDetector> componentDetector2Mock;
    private Mock<IComponentDetector> componentDetector3Mock;
    private Mock<IComponentDetector> versionedComponentDetector1Mock;

    private DetectorListingCommandService serviceUnderTest;

    private List<string> logOutput;

    [TestInitialize]
    public void InitializeTest()
    {
        this.loggerMock = new Mock<ILogger<DetectorListingCommandService>>();
        this.detectorsMock = new Mock<IEnumerable<IComponentDetector>>();
        this.componentDetector2Mock = new Mock<IComponentDetector>();
        this.componentDetector3Mock = new Mock<IComponentDetector>();
        this.versionedComponentDetector1Mock = new Mock<IComponentDetector>();

        this.serviceUnderTest = new DetectorListingCommandService(
            this.detectorsMock.Object,
            this.loggerMock.Object);

        this.logOutput = new List<string>();
        this.loggerMock.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation =>
            {
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var logMessage = (string)invokeMethod?.Invoke(formatter, new[] { state, exception });

                this.logOutput.Add(logMessage);
            }));

        this.componentDetector2Mock.SetupGet(x => x.Id).Returns("ComponentDetector2");
        this.componentDetector3Mock.SetupGet(x => x.Id).Returns("ComponentDetector3");
        this.versionedComponentDetector1Mock.SetupGet(x => x.Id).Returns("VersionedComponentDetector");

        IEnumerable<IComponentDetector> registeredDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,

            this.versionedComponentDetector1Mock.Object,
        };

        this.detectorsMock.Setup(x => x.GetEnumerator())
            .Returns(registeredDetectors.GetEnumerator());
    }

    [TestCleanup]
    public void CleanupTests()
    {
        this.detectorsMock.VerifyAll();
    }

    [TestMethod]
    public async Task DetectorListingCommandService_ListsDetectorsAsync()
    {
        var result = await this.serviceUnderTest.HandleAsync(new ListDetectionArgs());
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.logOutput.Should().Contain("ComponentDetector2");
        this.logOutput.Should().Contain("ComponentDetector3");
        this.logOutput.Should().Contain("VersionedComponentDetector");
    }
}
