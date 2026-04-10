namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NuGetCentralPackageManagementDetectorExperimentTests
{
    private readonly NuGetCentralPackageManagementDetectorExperiment experiment = new();

    [TestMethod]
    public void IsInControlGroup_ReturnsTrue_ForNuGetComponentDetector()
    {
        var nugetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.IsInControlGroup(nugetDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_ReturnsFalse_ForNuGetCentralPackageManagementDetector()
    {
        var centralPackageDetector = new NuGetCentralPackageManagementDetector(null, null, null);
        this.experiment.IsInControlGroup(centralPackageDetector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_ReturnsTrue_ForNuGetCentralPackageManagementDetector()
    {
        var centralPackageDetector = new NuGetCentralPackageManagementDetector(null, null, null);
        this.experiment.IsInExperimentGroup(centralPackageDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInExperimentGroup_ReturnsFalse_ForNuGetComponentDetector()
    {
        var nugetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.IsInExperimentGroup(nugetDetector).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRecord_AlwaysReturnsTrue()
    {
        var nugetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.ShouldRecord(nugetDetector, 0).Should().BeTrue();
    }
}
