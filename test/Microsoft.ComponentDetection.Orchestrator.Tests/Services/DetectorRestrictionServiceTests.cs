﻿namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;
using System;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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

    [TestInitialize]
    public void TestInitialize()
    {
        this.logger = new Mock<Logger>();
        this.firstDetectorMock = GenerateDetector("FirstDetector");
        this.secondDetectorMock = GenerateDetector("SecondDetector");
        this.thirdDetectorMock = GenerateDetector("ThirdDetector");
        this.retiredNpmDetector = GenerateDetector("MSLicenseDevNpm");
        this.newNpmDetector = GenerateDetector("NpmWithRoots");

        this.detectors = new[]
        {
            this.firstDetectorMock.Object, this.secondDetectorMock.Object,
            this.thirdDetectorMock.Object,
            this.retiredNpmDetector.Object,

            this.newNpmDetector.Object,
        };

        this.serviceUnderTest = new DetectorRestrictionService() { Logger = this.logger.Object };
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
        var detectorMock = GenerateDetector("defaultOffDetector");
        var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
        this.detectors = this.detectors.Union(new[] { defaultOffDetectorMock.Object as IComponentDetector }).ToArray();
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().NotContain(defaultOffDetectorMock.Object);
    }

    [TestMethod]
    public void WithRestrictions_AllowsDefaultOffWithDetectorRestriction()
    {
        var r = new DetectorRestrictions();
        var detectorMock = GenerateDetector("defaultOffDetector");
        var defaultOffDetectorMock = detectorMock.As<IDefaultOffComponentDetector>();
        this.detectors = this.detectors.Union(new[] { defaultOffDetectorMock.Object as IComponentDetector }).ToArray();
        r.ExplicitlyEnabledDetectorIds = new[] { "defaultOffDetector" };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(defaultOffDetectorMock.Object);
    }

    [TestMethod]
    public void WithRestrictions_FiltersBasedOnDetectorId()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorIds = new[] { "FirstDetector", "SecondDetector" },
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object).And.Contain(this.secondDetectorMock.Object)
            .And.NotContain(this.thirdDetectorMock.Object);

        r.AllowedDetectorIds = new[] { "NotARealDetector" };
        Action shouldThrow = () => this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        shouldThrow.Should().Throw<InvalidDetectorFilterException>();
    }

    [TestMethod]
    public void WithRestrictions_CorrectsRetiredDetector()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorIds = new[] { "MSLicenseDevNpm" },
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.newNpmDetector.Object);

        r.AllowedDetectorIds = new[] { "mslicensenpm" };
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.newNpmDetector.Object);

        r.AllowedDetectorIds = new[] { "mslicensenpm", "NpmWithRoots" };
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().OnlyContain(item => item == this.newNpmDetector.Object);
    }

    [TestMethod]
    public void WithRestrictions_FiltersBasedOnCategory()
    {
        var r = new DetectorRestrictions
        {
            AllowedDetectorCategories = new[] { "FirstDetectorCategory", "ThirdDetectorCategory" },
        };
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object).And.Contain(this.thirdDetectorMock.Object)
            .And.NotContain(this.secondDetectorMock.Object);

        r.AllowedDetectorCategories = new[] { "AllCategory" };
        restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
        restrictedDetectors
            .Should().Contain(this.firstDetectorMock.Object)
            .And.Contain(this.thirdDetectorMock.Object)
            .And.Contain(this.secondDetectorMock.Object);

        r.AllowedDetectorCategories = new[] { "NoCategory" };
        Action shouldThrow = () => this.serviceUnderTest.ApplyRestrictions(r, this.detectors);
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
        var restrictedDetectors = this.serviceUnderTest.ApplyRestrictions(r, detectors);
        restrictedDetectors
            .Should().Contain(detectors[2])
            .And.NotContain(detectors[0])
            .And.NotContain(detectors[1]);

        r.AllowedDetectorCategories = new[] { "Cat1" };
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

    private static Mock<IComponentDetector> GenerateDetector(string detectorName, string[] categories = null)
    {
        var mockDetector = new Mock<IComponentDetector>();
        mockDetector.SetupGet(x => x.Id).Returns($"{detectorName}");
        categories ??= new[] { $"{detectorName}Category", "AllCategory" };

        mockDetector.SetupGet(x => x.Categories).Returns(categories);
        return mockDetector;
    }
}
