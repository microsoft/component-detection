#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComponentDetectorTests
{
    private List<IComponentDetector> detectors;

    [TestInitialize]
    public void Initialize()
    {
        var serviceProvider = new ServiceCollection()
            .AddComponentDetection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();

        this.detectors = serviceProvider.GetServices<IComponentDetector>().ToList();
    }

    [TestMethod]
    public void AllDetectorsHaveUniqueIds()
    {
        var ids = this.detectors.Select(detector => detector.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void AllDetectorsHavePositiveVersion()
    {
        foreach (var detector in this.detectors)
        {
            detector.Version.Should().BePositive($"because {detector.Id} should be > 0");
        }
    }

    [TestMethod]
    public void AllDetectorsHaveUniqueCategories()
    {
        foreach (var detector in this.detectors)
        {
            detector.Categories.Should().OnlyHaveUniqueItems($"because {detector.Id} should have unique categories");
        }
    }

    [TestMethod]
    public void AllDetectorsHaveUniqueSupportedComponentTypes()
    {
        foreach (var detector in this.detectors)
        {
            detector.SupportedComponentTypes.Should().OnlyHaveUniqueItems($"because {detector.Id} should have unique supported component types");
        }
    }

    [TestMethod]
    public void UvLockComponentDetector_ImplementsIExperimentalDetector()
    {
        var uvLockDetector = this.detectors.SingleOrDefault(d => d.Id == "UvLock");

        uvLockDetector.Should().NotBeNull("because UvLockComponentDetector should be registered");
        uvLockDetector.Should().BeAssignableTo<IExperimentalDetector>("because UvLockComponentDetector should implement IExperimentalDetector");
    }
}
