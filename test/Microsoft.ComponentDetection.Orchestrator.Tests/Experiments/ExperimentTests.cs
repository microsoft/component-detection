namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ExperimentTests
{
    [TestMethod]
    public void Experiment_AddsToOnlyControlGroup()
    {
        var experiment = new Experiment();
        var testComponents = ExperimentTestUtils.CreateRandomComponents();

        experiment.AddComponentsToControlGroup(testComponents);

        experiment.ControlGroupComponents.Should().HaveCount(testComponents.Count);
        experiment.ExperimentGroupComponents.Should().BeEmpty();
    }

    [TestMethod]
    public void Experiment_AddsToOnlyExperimentGroup()
    {
        var experiment = new Experiment();
        var testComponents = ExperimentTestUtils.CreateRandomComponents();

        experiment.AddComponentsToExperimentalGroup(testComponents);

        experiment.ControlGroupComponents.Should().BeEmpty();
        experiment.ExperimentGroupComponents.Should().HaveCount(testComponents.Count);
    }
}
