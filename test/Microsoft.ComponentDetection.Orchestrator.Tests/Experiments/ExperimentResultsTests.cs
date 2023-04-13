namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ExperimentResultsTests
{
    [TestMethod]
    public void ExperimentResults_AddsToOnlyControlGroup()
    {
        var experiment = new ExperimentResults();
        var testComponents = ExperimentTestUtils.CreateRandomComponents();

        experiment.AddComponentsToControlGroup(testComponents);

        experiment.ControlGroupComponents.Should().HaveCount(testComponents.Count);
        experiment.ExperimentGroupComponents.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentResults_AddsToOnlyExperimentGroup()
    {
        var experiment = new ExperimentResults();
        var testComponents = ExperimentTestUtils.CreateRandomComponents();

        experiment.AddComponentsToExperimentalGroup(testComponents);

        experiment.ControlGroupComponents.Should().BeEmpty();
        experiment.ExperimentGroupComponents.Should().HaveCount(testComponents.Count);
    }
}
