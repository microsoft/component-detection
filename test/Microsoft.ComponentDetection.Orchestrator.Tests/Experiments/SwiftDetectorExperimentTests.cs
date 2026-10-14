namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.CocoaPods;
using Microsoft.ComponentDetection.Detectors.Swift;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class SwiftDetectorExperimentTests
{
    private readonly SwiftDetectorExperiment experiment = new();
    private readonly SwiftResolvedComponentDetector swiftDetector = new(null!, null!, null!);
    private readonly PodComponentDetector podDetector = new(null!, null!, null!);

    [TestMethod]
    public void IsInControlGroup_AlwaysReturnsFalse()
    {
        this.experiment.IsInControlGroup(this.swiftDetector).Should().BeFalse();
        this.experiment.IsInControlGroup(this.podDetector).Should().BeFalse();
        this.experiment.IsInControlGroup(Mock.Of<IComponentDetector>()).Should().BeFalse();
    }

    [TestMethod]
    public void IsInExperimentGroup_SwiftDetector_ReturnsTrue()
    {
        this.experiment.IsInExperimentGroup(this.swiftDetector).Should().BeTrue();
    }

    [TestMethod]
    public void IsInExperimentGroup_OtherDetectors_ReturnsFalse()
    {
        this.experiment.IsInExperimentGroup(this.podDetector).Should().BeFalse();
        this.experiment.IsInExperimentGroup(Mock.Of<IComponentDetector>()).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void ShouldRecord_AlwaysReturnsTrue(int numComponents)
    {
        this.experiment.ShouldRecord(this.swiftDetector, numComponents).Should().BeTrue();
        this.experiment.ShouldRecord(this.podDetector, numComponents).Should().BeTrue();
        this.experiment.ShouldRecord(Mock.Of<IComponentDetector>(), numComponents).Should().BeTrue();
    }
}
