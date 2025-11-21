namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Npm;
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
    public void IsInControlGroup_NpmComponentDetector_ReturnsTrue()
    {
        var npmDetector = new NpmComponentDetector(null, null, null);
        this.experiment.IsInControlGroup(npmDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_NpmLockfile3Detector_ReturnsTrue()
    {
        var npmLockfile3Detector = new NpmLockfile3Detector(null, null, null, null);
        this.experiment.IsInControlGroup(npmLockfile3Detector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_NpmComponentDetectorWithRoots_ReturnsTrue()
    {
        var npmDetectorWithRoots = new NpmComponentDetectorWithRoots(null, null, null, null);
        this.experiment.IsInControlGroup(npmDetectorWithRoots).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_PipReportComponentDetector_ReturnsTrue()
    {
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
    public void IsInExperimentGroup_NpmComponentDetector_ReturnsFalse()
    {
        var npmDetector = new NpmComponentDetector(null, null, null);
        this.experiment.IsInExperimentGroup(npmDetector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_NpmLockfile3Detector_ReturnsFalse()
    {
        var npmLockfile3Detector = new NpmLockfile3Detector(null, null, null, null);
        this.experiment.IsInExperimentGroup(npmLockfile3Detector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_NpmComponentDetectorWithRoots_ReturnsFalse()
    {
        var npmDetectorWithRoots = new NpmComponentDetectorWithRoots(null, null, null, null);
        this.experiment.IsInExperimentGroup(npmDetectorWithRoots).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_PipReportComponentDetector_ReturnsFalse()
    {
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

        this.experiment.IsInExperimentGroup(pipDetector).Should().BeFalse();
    }
}
