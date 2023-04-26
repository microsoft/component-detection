namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
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

    [TestMethod]
    public void ExperimentResults_DoesntAddDuplicateIds()
    {
        var testComponent = new NpmComponent("test", "1.0.0");
        var componentA = new DetectedComponent(testComponent);
        var componentB = new DetectedComponent(testComponent) { DevelopmentDependency = true };

        var experiment = new ExperimentResults();
        experiment.AddComponentsToControlGroup(new[] { componentA, componentB });

        experiment.ControlGroupComponents.Should().HaveCount(1);
        experiment.ExperimentGroupComponents.Should().BeEmpty();
        experiment.ControlGroupComponents.First().Should().BeEquivalentTo(new ExperimentComponent(componentA));
    }
}
