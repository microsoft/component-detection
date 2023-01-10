using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectorListingCommandServiceTests
{
    private Mock<ILogger> loggerMock;
    private Mock<IDetectorRegistryService> detectorRegistryServiceMock;
    private Mock<IComponentDetector> componentDetector2Mock;
    private Mock<IComponentDetector> componentDetector3Mock;
    private Mock<IComponentDetector> versionedComponentDetector1Mock;

    private DetectorListingCommandService serviceUnderTest;

    private List<string> logOutput;

    [TestInitialize]
    public void InitializeTest()
    {
        this.loggerMock = new Mock<ILogger>();
        this.detectorRegistryServiceMock = new Mock<IDetectorRegistryService>();
        this.componentDetector2Mock = new Mock<IComponentDetector>();
        this.componentDetector3Mock = new Mock<IComponentDetector>();
        this.versionedComponentDetector1Mock = new Mock<IComponentDetector>();

        this.serviceUnderTest = new DetectorListingCommandService
        {
            DetectorRegistryService = this.detectorRegistryServiceMock.Object,
            Logger = this.loggerMock.Object,
        };

        this.logOutput = new List<string>();
        this.loggerMock.Setup(x => x.LogInfo(It.IsAny<string>())).Callback<string>(loggedString =>
        {
            this.logOutput.Add(loggedString);
        });

        this.componentDetector2Mock.SetupGet(x => x.Id).Returns("ComponentDetector2");
        this.componentDetector3Mock.SetupGet(x => x.Id).Returns("ComponentDetector3");
        this.versionedComponentDetector1Mock.SetupGet(x => x.Id).Returns("VersionedComponentDetector");

        var registeredDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,

            this.versionedComponentDetector1Mock.Object,
        };

        this.detectorRegistryServiceMock.Setup(x => x.GetDetectors(It.IsAny<IEnumerable<DirectoryInfo>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .Returns(registeredDetectors);
    }

    [TestCleanup]
    public void CleanupTests()
    {
        this.detectorRegistryServiceMock.VerifyAll();
    }

    [TestMethod]
    public async Task DetectorListingCommandService_ListsDetectors()
    {
        var result = await this.serviceUnderTest.Handle(new ListDetectionArgs());
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.logOutput.Should().Contain("ComponentDetector2");
        this.logOutput.Should().Contain("ComponentDetector3");
        this.logOutput.Should().Contain("VersionedComponentDetector");
    }
}
