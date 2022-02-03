using System;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class DetectorRestrictionServiceTests
    {
        private Mock<Logger> logger;
        private Mock<IComponentDetector> firstDetectorMock;
        private Mock<IComponentDetector> secondDetectorMock;
        private Mock<IComponentDetector> thirdDetectorMock;
        private Mock<IComponentDetector> retiredNpmDetector;
        private Mock<IComponentDetector> newNpmDetector;
        private IComponentDetector[] detectors;
        private DetectorRestrictionService serviceUnderTest;

        private Mock<IComponentDetector> GenerateDetector(string detectorName, string[] categories = null)
        {
            var mockDetector = new Mock<IComponentDetector>();
            mockDetector.SetupGet(x => x.Id).Returns($"{detectorName}");
            if (categories == null)
            {
                categories = new[] { $"{detectorName}Category", "AllCategory" };
            }

            mockDetector.SetupGet(x => x.Categories).Returns(categories);
            return mockDetector;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Mock<Logger>();
            firstDetectorMock = GenerateDetector("FirstDetector");
            secondDetectorMock = GenerateDetector("SecondDetector");
            thirdDetectorMock = GenerateDetector("ThirdDetector");
            retiredNpmDetector = GenerateDetector("MSLicenseDevNpm");
            newNpmDetector = GenerateDetector("NpmWithRoots");

            detectors = new[] { firstDetectorMock.Object, secondDetectorMock.Object, thirdDetectorMock.Object, retiredNpmDetector.Object, newNpmDetector.Object };
            serviceUnderTest = new DetectorRestrictionService() { Logger = logger.Object };
        }

        [TestMethod]
        public void WithRestrictions_BaseCaseReturnsAll()
        {
            DetectorRestrictions r = new DetectorRestrictions();
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(detectors);
        }

        [TestMethod]
        public void WithRestrictions_RemovesDefaultOff()
        {
            DetectorRestrictions r = new DetectorRestrictions();
            var detectorMock = GenerateDetector("defaultOffDetector");
            var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
            detectors = detectors.Union(new[] { defaultOffDetectorMock.Object as IComponentDetector }).ToArray();
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().NotContain(defaultOffDetectorMock.Object as IComponentDetector);
        }

        [TestMethod]
        public void WithRestrictions_AllowsDefaultOffWithDetectorRestriction()
        {
            DetectorRestrictions r = new DetectorRestrictions();
            var detectorMock = GenerateDetector("defaultOffDetector");
            var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
            detectors = detectors.Union(new[] { defaultOffDetectorMock.Object as IComponentDetector }).ToArray();
            r.ExplicitlyEnabledDetectorIds = new[] { "defaultOffDetector" };
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(defaultOffDetectorMock.Object as IComponentDetector);
        }

        [TestMethod]
        public void WithRestrictions_FiltersBasedOnDetectorId()
        {
            var r = new DetectorRestrictions
            {
                AllowedDetectorIds = new[] { "FirstDetector", "SecondDetector" },
            };
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(firstDetectorMock.Object).And.Contain(secondDetectorMock.Object)
                .And.NotContain(thirdDetectorMock.Object);

            r.AllowedDetectorIds = new[] { "NotARealDetector" };
            Action shouldThrow = () => serviceUnderTest.ApplyRestrictions(r, detectors);
            shouldThrow.Should().Throw<InvalidDetectorFilterException>();
        }

        [TestMethod]
        public void WithRestrictions_CorrectsRetiredDetector()
        {
            var r = new DetectorRestrictions
            {
                AllowedDetectorIds = new[] { "MSLicenseDevNpm" },
            };
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(newNpmDetector.Object);

            r.AllowedDetectorIds = new[] { "mslicensenpm" };
            restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(newNpmDetector.Object);

            r.AllowedDetectorIds = new[] { "mslicensenpm", "NpmWithRoots" };
            restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().OnlyContain(item => item == newNpmDetector.Object);
        }

        [TestMethod]
        public void WithRestrictions_FiltersBasedOnCategory()
        {
            var r = new DetectorRestrictions
            {
                AllowedDetectorCategories = new[] { "FirstDetectorCategory", "ThirdDetectorCategory" },
            };
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(firstDetectorMock.Object).And.Contain(thirdDetectorMock.Object)
                .And.NotContain(secondDetectorMock.Object);

            r.AllowedDetectorCategories = new[] { "AllCategory" };
            restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(firstDetectorMock.Object)
                .And.Contain(thirdDetectorMock.Object)
                .And.Contain(secondDetectorMock.Object);

            r.AllowedDetectorCategories = new[] { "NoCategory" };
            Action shouldThrow = () => serviceUnderTest.ApplyRestrictions(r, detectors);
            shouldThrow.Should().Throw<InvalidDetectorCategoriesException>();
        }

        [TestMethod]
        public void WithRestrictions_AlwaysIncludesDetectorsThatSpecifyAllCategory()
        {
            var detectors = new[]
            {
                GenerateDetector("1", new[] { "Cat1" }).Object,
                GenerateDetector("2", new[] { "Cat2" }).Object,
                GenerateDetector("3", new[] { nameof(DetectorClass.All) }).Object,
            };

            var r = new DetectorRestrictions
            {
                AllowedDetectorCategories = new[] { "ACategoryWhichDoesntMatch" },
            };
            var restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(detectors[2])
                .And.NotContain(detectors[0])
                .And.NotContain(detectors[1]);

            r.AllowedDetectorCategories = new[] { "Cat1" };
            restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(detectors[0])
                .And.Contain(detectors[2])
                .And.NotContain(detectors[1]);

            r.AllowedDetectorCategories = null;
            restrictedDetectors = serviceUnderTest.ApplyRestrictions(r, detectors);
            restrictedDetectors
                .Should().Contain(detectors[0])
                .And.Contain(detectors[1])
                .And.Contain(detectors[2]);
        }
    }
}
