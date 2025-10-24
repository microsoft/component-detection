#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
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
        var diff = new ExperimentDiff([], testComponents);

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
        var diff = new ExperimentDiff(testComponents, []);

        diff.RemovedIds.Should().BeEquivalentTo(testComponents.Select(x => x.Id));
        diff.AddedIds.Should().BeEmpty();

        diff.DevelopmentDependencyChanges.Should().BeEmpty();
        diff.AddedRootIds.Should().BeEmpty();
        diff.RemovedRootIds.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsDevDependencies()
    {
        var componentA = ExperimentTestUtils.CreateRandomScannedComponent();
        var componentB = new ScannedComponent()
        {
            Component = componentA.Component,
        };

        componentA.IsDevelopmentDependency = false;
        componentB.IsDevelopmentDependency = true;

        var diff = new ExperimentDiff(
            [new ExperimentComponent(componentA)],
            [new ExperimentComponent(componentB)]);

        diff.DevelopmentDependencyChanges.Should().ContainSingle();

        var change = diff.DevelopmentDependencyChanges.First();
        change.Id.Should().Be(componentA.Component.Id);
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
        var rootComponent = ExperimentTestUtils.CreateRandomTypedComponent();
        var componentA = ExperimentTestUtils.CreateRandomScannedComponent();

        var componentB = new ScannedComponent()
        {
            Component = componentA.Component,
            TopLevelReferrers = [rootComponent],
        };

        var diff = new ExperimentDiff(
            [new ExperimentComponent(componentA),],
            [new ExperimentComponent(componentB),]);

        diff.AddedRootIds.Should().ContainSingle();
        diff.RemovedRootIds.Should().BeEmpty();

        var addedRoot = diff.AddedRootIds[componentA.Component.Id];
        addedRoot.Should().ContainSingle().And.BeEquivalentTo(rootComponent.Id);

        diff.AddedIds.Should().BeEmpty();
        diff.RemovedIds.Should().BeEmpty();
        diff.DevelopmentDependencyChanges.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsRemovedRootIds()
    {
        var rootComponent = ExperimentTestUtils.CreateRandomTypedComponent();
        var componentA = ExperimentTestUtils.CreateRandomScannedComponent();

        var componentB = new ScannedComponent()
        {
            Component = componentA.Component,
            TopLevelReferrers = [rootComponent],
        };

        var diff = new ExperimentDiff(
            [new ExperimentComponent(componentB),],
            [new ExperimentComponent(componentA),]);

        diff.RemovedRootIds.Should().ContainSingle();
        diff.AddedRootIds.Should().BeEmpty();

        var removedRoot = diff.RemovedRootIds[componentA.Component.Id];
        removedRoot.Should().ContainSingle().And.BeEquivalentTo(rootComponent.Id);

        diff.AddedIds.Should().BeEmpty();
        diff.RemovedIds.Should().BeEmpty();
        diff.DevelopmentDependencyChanges.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_MultipleIds_ShouldntThrow()
    {
        var componentA = ExperimentTestUtils.CreateRandomScannedComponent();
        var componentB = new ScannedComponent()
        {
            Component = componentA.Component,
            IsDevelopmentDependency = true,
        };

        var controlGroup = new[] { componentA, componentB }.Select(x => new ExperimentComponent(x));
        var experimentGroup = new[] { componentA, componentB }.Select(x => new ExperimentComponent(x));

        var action = () => new ExperimentDiff(controlGroup, experimentGroup);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ExperimentDiff_DiffsAddedDevDependenciesMissingInControlGroup()
    {
        var componentA = ExperimentTestUtils.CreateRandomScannedComponent();
        componentA.IsDevelopmentDependency = true;

        var diff = new ExperimentDiff(
            [],
            [new ExperimentComponent(componentA)]);

        diff.DevelopmentDependencyChanges.Should().ContainSingle();

        var change = diff.DevelopmentDependencyChanges.First();
        change.Id.Should().Be(componentA.Component.Id);
        change.OldValue.Should().BeFalse();
        change.NewValue.Should().BeTrue();

        diff.AddedIds.Should().BeEquivalentTo([componentA.Component.Id]);
        diff.RemovedIds.Should().BeEmpty();
        diff.AddedRootIds.Should().BeEmpty();
        diff.RemovedRootIds.Should().BeEmpty();
    }
}
