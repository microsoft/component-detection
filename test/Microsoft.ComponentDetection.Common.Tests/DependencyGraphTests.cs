#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DependencyGraphTests
{
    private DependencyGraph dependencyGraph;
    private IComponentRecorder componentRecorder;

    [TestInitialize]
    public void TestInitializer()
    {
        // Default value of true -- some tests will create their own, though.
        this.dependencyGraph = new DependencyGraph(true);
        this.componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
    }

    [TestMethod]
    public void AddComponent_ParentComponentIdIsPresent_DependencyRelationIsAdded()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };
        var componentD = new DependencyGraph.ComponentRefNode { Id = "componentD" };

        this.dependencyGraph.AddComponent(componentD);
        this.dependencyGraph.AddComponent(componentB, parentComponentId: componentD.Id);
        this.dependencyGraph.AddComponent(componentC, parentComponentId: componentB.Id);
        this.dependencyGraph.AddComponent(componentA, parentComponentId: componentB.Id);
        this.dependencyGraph.AddComponent(componentA, parentComponentId: componentC.Id);

        var componentAChildren = this.dependencyGraph.GetDependenciesForComponent(componentA.Id);
        componentAChildren.Should().BeEmpty();

        var componentBChildren = this.dependencyGraph.GetDependenciesForComponent(componentB.Id);
        componentBChildren.Should().HaveCount(2);
        componentBChildren.Should().Contain(componentA.Id);
        componentBChildren.Should().Contain(componentC.Id);

        var componentCChildren = this.dependencyGraph.GetDependenciesForComponent(componentC.Id);
        componentCChildren.Should().ContainSingle();
        componentCChildren.Should().Contain(componentA.Id);

        var componentDChildren = this.dependencyGraph.GetDependenciesForComponent(componentD.Id);
        componentDChildren.Should().ContainSingle();
        componentDChildren.Should().Contain(componentB.Id);
    }

    [TestMethod]
    public void AddComponent_parentComponentIdIsNotPresent_AdditionTakePlaceWithoutThrowing()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };

        Action action = () => this.dependencyGraph.AddComponent(componentA);
        action.Should().NotThrow();

        this.dependencyGraph.Contains(componentA.Id).Should().BeTrue();
    }

    [TestMethod]
    public void AddComponent_ComponentIsNull_ArgumentNullExceptionIsThrow()
    {
        Action action = () => this.dependencyGraph.AddComponent(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddComponent_ComponentHasNoId_ArgumentNullExceptionIsThrow()
    {
        var component = new DependencyGraph.ComponentRefNode { Id = null };
        Action action = () => this.dependencyGraph.AddComponent(component);
        action.Should().Throw<ArgumentNullException>();

        component = new DependencyGraph.ComponentRefNode { Id = string.Empty };
        action = () => this.dependencyGraph.AddComponent(component);
        action.Should().Throw<ArgumentNullException>();

        component = new DependencyGraph.ComponentRefNode { Id = "   " };
        action = () => this.dependencyGraph.AddComponent(component);
        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddComponent_ParentComponentWasNotAddedPreviously_ArgumentExceptionIsThrown()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };

        Action action = () => this.dependencyGraph.AddComponent(componentA, parentComponentId: "nonexistingComponent");

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetExplicitReferencedDependencyIds_ComponentsWereAddedSpecifyingRoot_RootsAreReturned()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };
        var componentD = new DependencyGraph.ComponentRefNode { Id = "componentD" };
        var componentE = new DependencyGraph.ComponentRefNode { Id = "componentE", IsExplicitReferencedDependency = true };
        var componentF = new DependencyGraph.ComponentRefNode { Id = "componentF" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC, componentB.Id);
        this.dependencyGraph.AddComponent(componentE);
        this.dependencyGraph.AddComponent(componentD, componentE.Id);
        this.dependencyGraph.AddComponent(componentC, componentD.Id);
        this.dependencyGraph.AddComponent(componentF, componentC.Id);

        var rootsForComponentA = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
        rootsForComponentA.Should().ContainSingle();

        var rootsForComponentE = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentE.Id);
        rootsForComponentE.Should().ContainSingle();

        var rootsForComponentB = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
        rootsForComponentB.Should().ContainSingle();
        rootsForComponentB.Should().Contain(componentA.Id);

        var rootsForComponentD = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentD.Id);
        rootsForComponentD.Should().ContainSingle();
        rootsForComponentD.Should().Contain(componentE.Id);

        var rootsForComponentC = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
        rootsForComponentC.Should().HaveCount(2);
        rootsForComponentC.Should().Contain(componentA.Id);
        rootsForComponentC.Should().Contain(componentE.Id);

        var rootsForComponentF = this.dependencyGraph.GetExplicitReferencedDependencyIds(componentF.Id);
        rootsForComponentF.Should().HaveCount(2);
        rootsForComponentF.Should().Contain(componentA.Id);
        rootsForComponentF.Should().Contain(componentE.Id);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_ComponentsWereAddedWithoutSpecifyingRoot_RootsAreEmpty(bool shouldUseTypedComponents)
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);

        var rootsForComponentA = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        rootsForComponentA.Should().BeEmpty();

        var rootsForComponentB = this.GetExplicitReferencedDependencyIds(componentB.Id, shouldUseTypedComponents);
        rootsForComponentB.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_ComponentIsRoot_ARootIsRootOfItSelf(bool shouldUseTypedComponents)
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
        this.dependencyGraph.AddComponent(componentA);

        var aRoots = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        aRoots.Should().ContainSingle();
        aRoots.Should().Contain(componentA.Id);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_RootHasParent_ReturnItselfAndItsParents(bool shouldUseTypedComponents)
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC, componentB.Id);

        var aRoots = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        aRoots.Should().ContainSingle();
        aRoots.Should().Contain(componentA.Id);

        var bRoots = this.GetExplicitReferencedDependencyIds(componentB.Id, shouldUseTypedComponents);
        bRoots.Should().HaveCount(2);
        bRoots.Should().Contain(componentA.Id);
        bRoots.Should().Contain(componentB.Id);

        var cRoots = this.GetExplicitReferencedDependencyIds(componentC.Id, shouldUseTypedComponents);
        cRoots.Should().HaveCount(3);
        cRoots.Should().Contain(componentA.Id);
        cRoots.Should().Contain(componentB.Id);
        cRoots.Should().Contain(componentC.Id);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_InsertionOrderNotAffectedRoots(bool shouldUseTypedComponents)
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        componentB = new DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
        this.dependencyGraph.AddComponent(componentB);

        var aRoots = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        aRoots.Should().HaveCount(2);
        aRoots.Should().Contain(componentA.Id);
        aRoots.Should().Contain(componentC.Id);

        var bRoots = this.GetExplicitReferencedDependencyIds(componentB.Id, shouldUseTypedComponents);
        bRoots.Should().HaveCount(3);
        bRoots.Should().Contain(componentA.Id);
        bRoots.Should().Contain(componentB.Id);
        bRoots.Should().Contain(componentC.Id);

        var cRoots = this.GetExplicitReferencedDependencyIds(componentC.Id, shouldUseTypedComponents);
        cRoots.Should().ContainSingle();
        cRoots.Should().Contain(componentC.Id);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_UseManualSelectionTurnedOff_ComponentsWithNoParentsAreSelectedAsExplicitReferencedDependencies(bool shouldUseTypedComponents)
    {
        this.dependencyGraph = new DependencyGraph(false);
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        var aRoots = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        aRoots.Should().ContainSingle();
        aRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentA.Id).Should().BeFalse();

        var bRoots = this.GetExplicitReferencedDependencyIds(componentB.Id, shouldUseTypedComponents);
        bRoots.Should().ContainSingle();
        bRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentB.Id).Should().BeFalse();

        var cRoots = this.GetExplicitReferencedDependencyIds(componentC.Id, shouldUseTypedComponents);
        cRoots.Should().ContainSingle();
        cRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentC.Id).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetExplicitReferencedDependencyIds_UseManualSelectionTurnedOff_PropertyIsExplicitReferencedDependencyIsIgnored(bool shouldUseTypedComponents)
    {
        this.dependencyGraph = new DependencyGraph(false);
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        var aRoots = this.GetExplicitReferencedDependencyIds(componentA.Id, shouldUseTypedComponents);
        aRoots.Should().ContainSingle();
        aRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentA.Id).Should().BeFalse();

        var bRoots = this.GetExplicitReferencedDependencyIds(componentB.Id, shouldUseTypedComponents);
        bRoots.Should().ContainSingle();
        bRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentB.Id).Should().BeFalse();

        var cRoots = this.GetExplicitReferencedDependencyIds(componentC.Id, shouldUseTypedComponents);
        cRoots.Should().ContainSingle();
        cRoots.Should().Contain(componentC.Id);
        ((IDependencyGraph)this.dependencyGraph).IsComponentExplicitlyReferenced(componentC.Id).Should().BeTrue();
    }

    [TestMethod]
    public void GetExplicitReferencedDependencyIds_NullComponentId_ArgumentNullExceptionIsThrown()
    {
        Action action = () => this.dependencyGraph.GetExplicitReferencedDependencyIds(null);
        action.Should().Throw<ArgumentNullException>();

        action = () => this.dependencyGraph.GetExplicitReferencedDependencyIds(string.Empty);
        action.Should().Throw<ArgumentNullException>();

        action = () => this.dependencyGraph.GetExplicitReferencedDependencyIds("   ");
        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GetExplicitReferencedDependencyIds_ComponentIdIsNotRegisteredInGraph_ArgumentExceptionIsThrown()
    {
        Action action = () => this.dependencyGraph.GetExplicitReferencedDependencyIds("nonExistingId");
        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void IsDevelopmentDependency_ReturnsAsExpected()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = false };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        this.dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(true);
        this.dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
        this.dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(null);
    }

    [TestMethod]
    public void IsDevelopmentDependency_ReturnsAsExpected_AfterMerge()
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = true };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = false };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        var componentANewValue = new DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = false };
        var componentBNewValue = new DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = true };
        var componentCNewValue = new DependencyGraph.ComponentRefNode { Id = "componentC", IsDevelopmentDependency = true };
        this.dependencyGraph.AddComponent(componentANewValue);
        this.dependencyGraph.AddComponent(componentBNewValue);
        this.dependencyGraph.AddComponent(componentCNewValue);

        this.dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(false);
        this.dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
        this.dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(true);

        var componentANullValue = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentBNullValue = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentCNullValue = new DependencyGraph.ComponentRefNode { Id = "componentC" };
        this.dependencyGraph.AddComponent(componentANullValue);
        this.dependencyGraph.AddComponent(componentBNullValue);
        this.dependencyGraph.AddComponent(componentCNullValue);

        this.dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(false);
        this.dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
        this.dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(true);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetAncestors_ReturnsAsExpected(bool shouldUseTypedComponents)
    {
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };

        this.dependencyGraph.AddComponent(componentA);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC, componentB.Id);

        var ancestors = this.GetAncestors(componentC.Id, shouldUseTypedComponents);
        ancestors.Should().HaveCount(2);
        ancestors.Should().Contain(componentA.Id);
        ancestors.Should().Contain(componentB.Id);

        ancestors = this.GetAncestors(componentB.Id, shouldUseTypedComponents);
        ancestors.Should().ContainSingle();
        ancestors.Should().Contain(componentA.Id);

        ancestors = this.GetAncestors(componentA.Id, shouldUseTypedComponents);
        ancestors.Should().BeEmpty();

        ancestors = this.GetAncestors("test", shouldUseTypedComponents);
        ancestors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetAncestors_Null_ThrowsException(bool shouldUseTypedComponents)
    {
        this.Invoking(d => d.GetAncestors(null, shouldUseTypedComponents)).Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetAncestors_Cyclic_ReturnsAsExpected(bool shouldUseTypedComponents)
    {
        var root = new DependencyGraph.ComponentRefNode { Id = "root" };
        var componentA = new DependencyGraph.ComponentRefNode { Id = "componentA" };
        var componentB = new DependencyGraph.ComponentRefNode { Id = "componentB" };
        var componentC = new DependencyGraph.ComponentRefNode { Id = "componentC" };

        // root -> componentA -> componentB -> componentC --> loops to componentA
        this.dependencyGraph.AddComponent(root);
        this.dependencyGraph.AddComponent(componentA, root.Id);
        this.dependencyGraph.AddComponent(componentB, componentA.Id);
        this.dependencyGraph.AddComponent(componentC, componentB.Id);
        this.dependencyGraph.AddComponent(componentA, componentC.Id);

        var ancestors = this.GetAncestors(componentC.Id, shouldUseTypedComponents);
        ancestors.Should().HaveCount(3);
        ancestors.Should().Contain(root.Id);
        ancestors.Should().Contain(componentA.Id);
        ancestors.Should().Contain(componentB.Id);

        ancestors = this.GetAncestors(componentA.Id, shouldUseTypedComponents);
        ancestors.Should().HaveCount(3);
        ancestors.Should().Contain(root.Id);
        ancestors.Should().Contain(componentB.Id);
        ancestors.Should().Contain(componentC.Id);

        ancestors = this.GetAncestors(componentB.Id, shouldUseTypedComponents);
        ancestors.Should().HaveCount(3);
        ancestors.Should().Contain(root.Id);
        ancestors.Should().Contain(componentC.Id);
        ancestors.Should().Contain(componentA.Id);

        ancestors = this.GetAncestors(root.Id, shouldUseTypedComponents);
        ancestors.Should().BeEmpty();
    }

    private IEnumerable<string> GetExplicitReferencedDependencyIds(string componentId, bool shouldUseTypedComponents)
    {
        this.dependencyGraph.FillTypedComponents(id => new NuGetComponent(id, "1.0.0"));
        return shouldUseTypedComponents
            ? this.dependencyGraph.GetRootsAsTypedComponents(componentId, id => new NuGetComponent(id, "1.0.0")).Select(x => ((NuGetComponent)x).Name)
            : this.dependencyGraph.GetExplicitReferencedDependencyIds(componentId);
    }

    private ICollection<string> GetAncestors(string componentId, bool shouldUseTypedComponents)
    {
        this.dependencyGraph.FillTypedComponents(id => new NuGetComponent(id, "1.0.0"));
        return shouldUseTypedComponents
            ? this.dependencyGraph.GetAncestorsAsTypedComponents(componentId, id => new NuGetComponent(id, "1.0.0")).Select(x => ((NuGetComponent)x).Name).ToList()
            : this.dependencyGraph.GetAncestors(componentId);
    }
}
