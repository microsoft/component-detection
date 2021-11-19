using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services
{
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
            loggerMock = new Mock<ILogger>();
            detectorRegistryServiceMock = new Mock<IDetectorRegistryService>();
            componentDetector2Mock = new Mock<IComponentDetector>();
            componentDetector3Mock = new Mock<IComponentDetector>();
            versionedComponentDetector1Mock = new Mock<IComponentDetector>();

            serviceUnderTest = new DetectorListingCommandService
            {
                DetectorRegistryService = detectorRegistryServiceMock.Object,
                Logger = loggerMock.Object,
            };

            logOutput = new List<string>();
            loggerMock.Setup(x => x.LogInfo(It.IsAny<string>())).Callback<string>(loggedString =>
            {
                logOutput.Add(loggedString);
            });

            componentDetector2Mock.SetupGet(x => x.Id).Returns("ComponentDetector2");
            componentDetector3Mock.SetupGet(x => x.Id).Returns("ComponentDetector3");
            versionedComponentDetector1Mock.SetupGet(x => x.Id).Returns("VersionedComponentDetector");

            var registeredDetectors = new[] { componentDetector2Mock.Object, componentDetector3Mock.Object, versionedComponentDetector1Mock.Object };
            detectorRegistryServiceMock.Setup(x => x.GetDetectors(It.IsAny<IEnumerable<DirectoryInfo>>(), It.IsAny<IEnumerable<string>>()))
                .Returns(registeredDetectors);
        }

        [TestCleanup]
        public void CleanupTests()
        {
            detectorRegistryServiceMock.VerifyAll();
        }

        [TestMethod]
        public async Task DetectorListingCommandService_ListsDetectors()
        {
            var result = await serviceUnderTest.Handle(new ListDetectionArgs());
            result.ResultCode.Should().Be(ProcessingResultCode.Success);

            logOutput.Should().Contain("ComponentDetector2");
            logOutput.Should().Contain("ComponentDetector3");
            logOutput.Should().Contain("VersionedComponentDetector");
        }
    }
}
