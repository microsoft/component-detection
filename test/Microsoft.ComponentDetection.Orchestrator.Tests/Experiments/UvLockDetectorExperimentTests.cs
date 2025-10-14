#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Uv;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UvLockDetectorExperimentTests
{
    private readonly UvLockDetectorExperiment experiment = new();

    [TestMethod]
    public void IsInControlGroup_ReturnsTrue_ForPipReportComponentDetector()
    {
        var pipReportDetector = new PipReportComponentDetector(null, null, null, null, null, null, null, null, null);
        this.experiment.IsInControlGroup(pipReportDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInControlGroup_ReturnsFalse_ForPipComponentDetector()
    {
        var pipDetector = new PipComponentDetector(null, null, null, null, null);
        this.experiment.IsInControlGroup(pipDetector).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_ReturnsTrue_ForUvLockComponentDetector()
    {
        var uvLockDetector = new UvLockComponentDetector(null, null, null);
        this.experiment.IsInExperimentGroup(uvLockDetector).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRecord_AlwaysReturnsTrue()
    {
        var pipDetector = new PipComponentDetector(null, null, null, null, null);
        this.experiment.ShouldRecord(pipDetector, 0).Should().BeTrue();
    }
}
