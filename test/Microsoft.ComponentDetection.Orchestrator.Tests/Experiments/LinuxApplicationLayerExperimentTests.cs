#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class LinuxApplicationLayerExperimentTests
{
    private readonly LinuxApplicationLayerExperiment experiment = new();

    [TestMethod]
    public void IsInControlGroup_LinuxContainerDetector_ReturnsTrue()
    {
        var linuxDetector = new LinuxContainerDetector(null, null, null);
        this.experiment.IsInControlGroup(linuxDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_FileBasedDetectors_ReturnsTrue()
    {
        var npmDetector = new NpmComponentDetector(null, null, null);
        this.experiment.IsInControlGroup(npmDetector).Should().BeTrue();

        var npmLockfile3Detector = new NpmLockfile3Detector(null, null, null, null);
        this.experiment.IsInControlGroup(npmLockfile3Detector).Should().BeTrue();

        var npmDetectorWithRoots = new NpmComponentDetectorWithRoots(null, null, null, null);
        this.experiment.IsInControlGroup(npmDetectorWithRoots).Should().BeTrue();

        var pipDetector = new PipReportComponentDetector(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        this.experiment.IsInControlGroup(pipDetector).Should().BeTrue();

        var nuGetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.IsInControlGroup(nuGetDetector).Should().BeTrue();

        var nuGetProjectCentricDetector = new NuGetProjectModelProjectCentricComponentDetector(
            null,
            null,
            null,
            null
        );
        this.experiment.IsInControlGroup(nuGetProjectCentricDetector).Should().BeTrue();

        var nuGetPackagesConfigDetector = new NuGetPackagesConfigDetector(null, null, null);
        this.experiment.IsInControlGroup(nuGetPackagesConfigDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_LinuxApplicationLayerDetector_ReturnsFalse()
    {
        var experimentalDetector = new LinuxApplicationLayerDetector(null, null, null);
        this.experiment.IsInControlGroup(experimentalDetector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_LinuxApplicationLayerDetector_ReturnsTrue()
    {
        var experimentalDetector = new LinuxApplicationLayerDetector(null, null, null);
        this.experiment.IsInExperimentGroup(experimentalDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInExperimentGroup_LinuxContainerDetector_ReturnsFalse()
    {
        var linuxDetector = new LinuxContainerDetector(null, null, null);
        this.experiment.IsInExperimentGroup(linuxDetector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_FileBasedDetectors_ReturnsTrue()
    {
        var npmDetector = new NpmComponentDetector(null, null, null);
        this.experiment.IsInExperimentGroup(npmDetector).Should().BeTrue();

        var npmLockfile3Detector = new NpmLockfile3Detector(null, null, null, null);
        this.experiment.IsInExperimentGroup(npmLockfile3Detector).Should().BeTrue();

        var npmDetectorWithRoots = new NpmComponentDetectorWithRoots(null, null, null, null);
        this.experiment.IsInExperimentGroup(npmDetectorWithRoots).Should().BeTrue();

        var pipDetector = new PipReportComponentDetector(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        this.experiment.IsInExperimentGroup(pipDetector).Should().BeTrue();

        var nuGetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.IsInExperimentGroup(nuGetDetector).Should().BeTrue();

        var nuGetProjectCentricDetector = new NuGetProjectModelProjectCentricComponentDetector(
            null,
            null,
            null,
            null
        );
        this.experiment.IsInExperimentGroup(nuGetProjectCentricDetector).Should().BeTrue();

        var nuGetPackagesConfigDetector = new NuGetPackagesConfigDetector(null, null, null);
        this.experiment.IsInExperimentGroup(nuGetPackagesConfigDetector).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRecord_ExperimentGroup_ReturnsTrue_WhenNumComponentsGreaterThanZero()
    {
        var experimentalDetector = new LinuxApplicationLayerDetector(null, null, null);
        this.experiment.ShouldRecord(experimentalDetector, 1).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRecord_ExperimentGroup_ReturnsFalse_WhenNumComponentsIsZero()
    {
        var experimentalDetector = new LinuxApplicationLayerDetector(null, null, null);
        this.experiment.ShouldRecord(experimentalDetector, 0).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRecord_ControlGroup_ReturnsTrue_WhenNumComponentsGreaterThanZero()
    {
        var linuxDetector = new LinuxContainerDetector(null, null, null);
        this.experiment.ShouldRecord(linuxDetector, 1).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRecord_ControlGroup_ReturnsFalse_WhenNumComponentsIsZero()
    {
        var linuxDetector = new LinuxContainerDetector(null, null, null);
        this.experiment.ShouldRecord(linuxDetector, 0).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRecord_FileBasedDetectors_AlwaysReturnsTrue()
    {
        var npmDetector = new NpmComponentDetector(null, null, null);
        this.experiment.ShouldRecord(npmDetector, 0).Should().BeTrue();

        var pipDetector = new PipReportComponentDetector(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        this.experiment.ShouldRecord(pipDetector, 0).Should().BeTrue();

        var nuGetDetector = new NuGetComponentDetector(null, null, null);
        this.experiment.ShouldRecord(nuGetDetector, 0).Should().BeTrue();

        var nuGetProjectCentricDetector = new NuGetProjectModelProjectCentricComponentDetector(
            null,
            null,
            null,
            null
        );
        this.experiment.ShouldRecord(nuGetProjectCentricDetector, 0).Should().BeTrue();

        var nuGetPackagesConfigDetector = new NuGetPackagesConfigDetector(null, null, null);
        this.experiment.ShouldRecord(nuGetPackagesConfigDetector, 0).Should().BeTrue();
    }
}
