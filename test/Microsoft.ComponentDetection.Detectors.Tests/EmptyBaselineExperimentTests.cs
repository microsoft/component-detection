namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Experiments;
using Microsoft.ComponentDetection.Detectors.Swift;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class EmptyBaselineExperimentTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void Constructor_RejectsMissingExperimentName(string? name)
    {
        var action = () => new EmptyBaselineExperiment<SwiftResolvedComponentDetector>(name!);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void IsInExperimentGroup_ExcludesUnrelatedDetector()
    {
        var experiment = new EmptyBaselineExperiment<SwiftResolvedComponentDetector>("SwiftDiscovery");

        experiment.IsInExperimentGroup(Mock.Of<IComponentDetector>()).Should().BeFalse();
    }
}
