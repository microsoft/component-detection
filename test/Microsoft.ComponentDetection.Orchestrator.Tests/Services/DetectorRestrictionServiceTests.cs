#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectorRestrictionServiceTests
{
    private Mock<ILogger<DetectorRestrictionService>> logger;
    private Mock<IComponentDetector> firstDetectorMock;
    private Mock<IComponentDetector> secondDetectorMock;
    private Mock<IComponentDetector> thirdDetectorMock;
    private Mock<IComponentDetector> retiredNpmDetector;
    private Mock<IComponentDetector> newNpmDetector;
    private IComponentDetector[] detectors;
    private DetectorRestrictionService serviceUnderTest;

    [TestInitialize]
    public void TestInitialize()
    {
        this.logger = new Mock<ILogger<DetectorRestrictionService>>();
        this.firstDetectorMock = this.GenerateDetector("FirstDetector");
        this.secondDetectorMock = this.GenerateDetector("SecondDetector");
        this.thirdDetectorMock = this.GenerateDetector("ThirdDetector");
        this.retiredNpmDetector = this.GenerateDetector("MSLicenseDevNpm");
        this.newNpmDetector = this.GenerateDetector("NpmWithRoots");

        this.detectors =
        [
            this.firstDetectorMock.Object,
            this.secondDetectorMock.Object,
            this.thirdDetectorMock.Object,
            this.retiredNpmDetector.Object,

            this.newNpmDetector.Object,
        ];

        this.serviceUnderTest = new DetectorRestrictionService(this.logger.Object);
    }

    [TestMethod]
    public void WithRestrictions_BaseCaseReturnsAll()
    {
        var r = new DetectorRestrictions();
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.detectors);
    }

    [TestMethod]
    public void WithRestrictions_RemovesDefaultOff()
    {
        var r = new DetectorRestrictions();
        var detectorMock = this.GenerateDetector("defaultOffDetector");
        var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
        this.detectors = this.detectors.Union([defaultOffDetectorMock.Object]).ToArray();
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().NotContain(defaultOffDetectorMock.Object);
    }

    [TestMethod]
    public void WithRestrictions_AllowsDefaultOffWithDetectorRestriction()
    {
        var r = new DetectorRestrictions();
        var detectorMock = this.GenerateDetector("defaultOffDetector");
        var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
        this.detectors = this.detectors.Union([defaultOffDetectorMock.Object]).ToArray();
        r.ExplicitlyEnabledDetectorIds = ["defaultOffDetector"];
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(defaultOffDetectorMock.Object);
    }

    [TestMethod]
    public void WithRestrictions_FiltersBasedOnDetectorId()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorIds = ["FirstDetector", "SecondDetector"],
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object).And.Contain(this.secondDetectorMock.Object)
            .And.NotContain(this.thirdDetectorMock.Object);

        r.AllowedDetectorIds = ["NotARealDetector"];
        Action shouldThrow = () => this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        shouldThrow.Should().Throw<InvalidDetectorFilterException>();
    }

    [TestMethod]
    public void WithRestrictions_CorrectsRetiredDetector()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorIds = ["MSLicenseDevNpm"],
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.newNpmDetector.Object);

        r.AllowedDetectorIds = ["mslicensenpm"];
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.newNpmDetector.Object);

        r.AllowedDetectorIds = ["mslicensenpm", "NpmWithRoots"];
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().OnlyContain(item => item == this.newNpmDetector.Object);
    }

    [TestMethod]
    public void WithRestrictions_FiltersBasedOnCategory()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorCategories = ["FirstDetectorCategory", "ThirdDetectorCategory"],
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object).And.Contain(this.thirdDetectorMock.Object)
            .And.NotContain(this.secondDetectorMock.Object);

        r.AllowedDetectorCategories = ["AllCategory"];
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object)
            .And.Contain(this.thirdDetectorMock.Object)
            .And.Contain(this.secondDetectorMock.Object);

        r.AllowedDetectorCategories = ["NoCategory"];
        Action shouldThrow = () => this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        shouldThrow.Should().Throw<InvalidDetectorCategoriesException>();
    }

    [TestMethod]
    public void WithRestrictions_AlwaysIncludesDetectorsThatSpecifyAllCategory()
    {
        var detectors = new[]
        {
            this.GenerateDetector("1", ["Cat1"]).Object,
            this.GenerateDetector("2", ["Cat2"]).Object,
            this.GenerateDetector("3", [nameof(DetectorClass.All)]).Object,
        };

        var r = new DetectorRestrictions
        {
            AllowedDetectorCategories = ["ACategoryWhichDoesntMatch"],
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, detectors);
        restrictedDetectors
            .Should().Contain(detectors[2])
            .And.NotContain(detectors[0])
            .And.NotContain(detectors[1]);

        r.AllowedDetectorCategories = ["Cat1"];
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, detectors);
        restrictedDetectors
            .Should().Contain(detectors[0])
            .And.Contain(detectors[2])
            .And.NotContain(detectors[1]);

        r.AllowedDetectorCategories = null;
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, detectors);
        restrictedDetectors
            .Should().Contain(detectors[0])
            .And.Contain(detectors[1])
            .And.Contain(detectors[2]);
    }

    private Mock<IComponentDetector> GenerateDetector(string detectorName, string[] categories = null)
    {
        var mockDetector = new Mock<IComponentDetector>();
        mockDetector.SetupGet(x => x.Id).Returns($"{detectorName}");
        categories ??= [$"{detectorName}Category", "AllCategory"];

        mockDetector.SetupGet(x => x.Categories).Returns(categories);
        return mockDetector;
    }
}
