namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ExperimentDiffTests
{
    [TestMethod]
    public void ExperimentDiff_DiffsAddedIds()
    {
        var testComponents = ExperimentTestUtils.CreateRandomExperimentComponents();
        var diff = new ExperimentDiff(Enumerable.Empty<ExperimentComponent>(), testComponents);

        diff.AddedIds.Should().BeEquivalentTo(testComponents.Select(x => x.Id));
        diff.RemovedIds.Should().BeEmpty();

        diff.DevelopmentDependencyChanges.Should().BeEmpty();
        diff.AddedRootIds.Should().BeEmpty();
        diff.RemovedRootIds.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsRemovedIds()
    {
        var testComponents = ExperimentTestUtils.CreateRandomExperimentComponents();
        var diff = new ExperimentDiff(testComponents, Enumerable.Empty<ExperimentComponent>());

        diff.RemovedIds.Should().BeEquivalentTo(testComponents.Select(x => x.Id));
        diff.AddedIds.Should().BeEmpty();

        diff.DevelopmentDependencyChanges.Should().BeEmpty();
        diff.AddedRootIds.Should().BeEmpty();
        diff.RemovedRootIds.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsDevDependencies()
    {
        var detectedComponent = ExperimentTestUtils.CreateRandomComponent();
        var componentA = new DetectedComponent(detectedComponent.Component);
        var componentB = new DetectedComponent(detectedComponent.Component);

        componentA.DevelopmentDependency = false;
        componentB.DevelopmentDependency = true;

        var diff = new ExperimentDiff(
            new[] { new ExperimentComponent(componentA) },
            new[] { new ExperimentComponent(componentB) });

        diff.DevelopmentDependencyChanges.Should().HaveCount(1);

        var change = diff.DevelopmentDependencyChanges.First();
        change.Id.Should().Be(detectedComponent.Component.Id);
        change.OldValue.Should().BeFalse();
        change.NewValue.Should().BeTrue();

        diff.AddedIds.Should().BeEmpty();
        diff.RemovedIds.Should().BeEmpty();
        diff.AddedRootIds.Should().BeEmpty();
        diff.RemovedRootIds.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsAddedRootIds()
    {
        var rootComponent = ExperimentTestUtils.CreateRandomComponent();
        var component = ExperimentTestUtils.CreateRandomComponent();

        var componentA = new DetectedComponent(component.Component);
        var componentB = new DetectedComponent(component.Component)
        {
            DependencyRoots = new HashSet<TypedComponent> { rootComponent.Component },
        };

        var diff = new ExperimentDiff(
            new[] { new ExperimentComponent(componentA), },
            new[] { new ExperimentComponent(componentB), });

        diff.AddedRootIds.Should().HaveCount(1);
        diff.RemovedRootIds.Should().BeEmpty();

        var addedRoot = diff.AddedRootIds[component.Component.Id];
        addedRoot.Should().HaveCount(1);
        addedRoot.Should().BeEquivalentTo(rootComponent.Component.Id);

        diff.AddedIds.Should().BeEmpty();
        diff.RemovedIds.Should().BeEmpty();
        diff.DevelopmentDependencyChanges.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsRemovedRootIds()
    {
        var rootComponent = ExperimentTestUtils.CreateRandomComponent();
        var component = ExperimentTestUtils.CreateRandomComponent();

        var componentA = new DetectedComponent(component.Component)
        {
            DependencyRoots = new HashSet<TypedComponent> { rootComponent.Component },
        };
        var componentB = new DetectedComponent(component.Component);

        var diff = new ExperimentDiff(
            new[] { new ExperimentComponent(componentA), },
            new[] { new ExperimentComponent(componentB), });

        diff.RemovedRootIds.Should().HaveCount(1);
        diff.AddedRootIds.Should().BeEmpty();

        var removedRoot = diff.RemovedRootIds[component.Component.Id];
        removedRoot.Should().HaveCount(1);
        removedRoot.Should().BeEquivalentTo(rootComponent.Component.Id);

        diff.AddedIds.Should().BeEmpty();
        diff.RemovedIds.Should().BeEmpty();
        diff.DevelopmentDependencyChanges.Should().BeEmpty();
    }
}
