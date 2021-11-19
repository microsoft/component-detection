using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    public class DependencyGraphTests
    {
        private DependencyGraph.DependencyGraph dependencyGraph;

        [TestInitialize]
        public void TestInitializer()
        {
            // Default value of true -- some tests will create their own, though.
            dependencyGraph = new DependencyGraph.DependencyGraph(true);
        }

        [TestMethod]
        public void AddComponent_ParentComponentIdIsPresent_DependencyRelationIsAdded()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA" };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };
            var componentD = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentD" };

            dependencyGraph.AddComponent(componentD);
            dependencyGraph.AddComponent(componentB, parentComponentId: componentD.Id);
            dependencyGraph.AddComponent(componentC, parentComponentId: componentB.Id);
            dependencyGraph.AddComponent(componentA, parentComponentId: componentB.Id);
            dependencyGraph.AddComponent(componentA, parentComponentId: componentC.Id);

            var componentAChildren = dependencyGraph.GetDependenciesForComponent(componentA.Id);
            componentAChildren.Should().HaveCount(0);

            var componentBChildren = dependencyGraph.GetDependenciesForComponent(componentB.Id);
            componentBChildren.Should().HaveCount(2);
            componentBChildren.Should().Contain(componentA.Id);
            componentBChildren.Should().Contain(componentC.Id);

            var componentCChildren = dependencyGraph.GetDependenciesForComponent(componentC.Id);
            componentCChildren.Should().HaveCount(1);
            componentCChildren.Should().Contain(componentA.Id);

            var componentDChildren = dependencyGraph.GetDependenciesForComponent(componentD.Id);
            componentDChildren.Should().HaveCount(1);
            componentDChildren.Should().Contain(componentB.Id);
        }

        [TestMethod]
        public void AddComponent_parentComponentIdIsNotPresent_AdditionTakePlaceWithoutThrowing()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };

            Action action = () => dependencyGraph.AddComponent(componentA);
            action.Should().NotThrow();

            dependencyGraph.Contains(componentA.Id).Should().BeTrue();
        }

        [TestMethod]
        public void AddComponent_ComponentIsNull_ArgumentNullExceptionIsThrow()
        {
            Action action = () => dependencyGraph.AddComponent(null);

            action.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void AddComponent_ComponentHasNoId_ArgumentNullExceptionIsThrow()
        {
            var component = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = null };
            Action action = () => dependencyGraph.AddComponent(component);
            action.Should().Throw<ArgumentNullException>();

            component = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = string.Empty };
            action = () => dependencyGraph.AddComponent(component);
            action.Should().Throw<ArgumentNullException>();

            component = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "   " };
            action = () => dependencyGraph.AddComponent(component);
            action.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void AddComponent_ParentComponentWasNotAddedPreviously_ArgumentExceptionIsThrown()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA" };

            Action action = () => dependencyGraph.AddComponent(componentA, parentComponentId: "nonexistingComponent");

            action.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_ComponentsWereAddedSpecifyingRoot_RootsAreReturned()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };
            var componentD = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentD" };
            var componentE = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentE", IsExplicitReferencedDependency = true };
            var componentF = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentF" };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC, componentB.Id);
            dependencyGraph.AddComponent(componentE);
            dependencyGraph.AddComponent(componentD, componentE.Id);
            dependencyGraph.AddComponent(componentC, componentD.Id);
            dependencyGraph.AddComponent(componentF, componentC.Id);

            var rootsForComponentA = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            rootsForComponentA.Should().HaveCount(1);

            var rootsForComponentE = dependencyGraph.GetExplicitReferencedDependencyIds(componentE.Id);
            rootsForComponentE.Should().HaveCount(1);

            var rootsForComponentB = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            rootsForComponentB.Should().HaveCount(1);
            rootsForComponentB.Should().Contain(componentA.Id);

            var rootsForComponentD = dependencyGraph.GetExplicitReferencedDependencyIds(componentD.Id);
            rootsForComponentD.Should().HaveCount(1);
            rootsForComponentD.Should().Contain(componentE.Id);

            var rootsForComponentC = dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
            rootsForComponentC.Should().HaveCount(2);
            rootsForComponentC.Should().Contain(componentA.Id);
            rootsForComponentC.Should().Contain(componentE.Id);

            var rootsForComponentF = dependencyGraph.GetExplicitReferencedDependencyIds(componentF.Id);
            rootsForComponentF.Should().HaveCount(2);
            rootsForComponentF.Should().Contain(componentA.Id);
            rootsForComponentF.Should().Contain(componentE.Id);
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_ComponentsWereAddedWithoutSpecifyingRoot_RootsAreEmpty()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA" };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);

            var rootsForComponentA = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            rootsForComponentA.Should().HaveCount(0);

            var rootsForComponentB = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            rootsForComponentB.Should().HaveCount(0);
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_ComponentIsRoot_ARootIsRootOfItSelf()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
            dependencyGraph.AddComponent(componentA);

            var aRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            aRoots.Should().HaveCount(1);
            aRoots.Should().Contain(componentA.Id);
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_RootHasParent_ReturnItselfAndItsParents()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC, componentB.Id);

            var aRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            aRoots.Should().HaveCount(1);
            aRoots.Should().Contain(componentA.Id);

            var bRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            bRoots.Should().HaveCount(2);
            bRoots.Should().Contain(componentA.Id);
            bRoots.Should().Contain(componentB.Id);

            var cRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
            cRoots.Should().HaveCount(3);
            cRoots.Should().Contain(componentA.Id);
            cRoots.Should().Contain(componentB.Id);
            cRoots.Should().Contain(componentC.Id);
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_InsertionOrderNotAffectedRoots()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC);
            dependencyGraph.AddComponent(componentA, componentC.Id);

            componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
            dependencyGraph.AddComponent(componentB);

            var aRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            aRoots.Should().HaveCount(2);
            aRoots.Should().Contain(componentA.Id);
            aRoots.Should().Contain(componentC.Id);

            var bRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            bRoots.Should().HaveCount(3);
            bRoots.Should().Contain(componentA.Id);
            bRoots.Should().Contain(componentB.Id);
            bRoots.Should().Contain(componentC.Id);

            var cRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
            cRoots.Should().HaveCount(1);
            cRoots.Should().Contain(componentC.Id);
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_UseManualSelectionTurnedOff_ComponentsWithNoParentsAreSelectedAsExplicitReferencedDependencies()
        {
            dependencyGraph = new DependencyGraph.DependencyGraph(false);
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA" };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC);
            dependencyGraph.AddComponent(componentA, componentC.Id);

            var aRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            aRoots.Should().HaveCount(1);
            aRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentA.Id).Should().BeFalse();

            var bRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            bRoots.Should().HaveCount(1);
            bRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentB.Id).Should().BeFalse();

            var cRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
            cRoots.Should().HaveCount(1);
            cRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentC.Id).Should().BeTrue();
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_UseManualSelectionTurnedOff_PropertyIsExplicitReferencedDependencyIsIgnored()
        {
            dependencyGraph = new DependencyGraph.DependencyGraph(false);
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsExplicitReferencedDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsExplicitReferencedDependency = true };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC", IsExplicitReferencedDependency = true };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC);
            dependencyGraph.AddComponent(componentA, componentC.Id);

            var aRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentA.Id);
            aRoots.Should().HaveCount(1);
            aRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentA.Id).Should().BeFalse();

            var bRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentB.Id);
            bRoots.Should().HaveCount(1);
            bRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentB.Id).Should().BeFalse();

            var cRoots = dependencyGraph.GetExplicitReferencedDependencyIds(componentC.Id);
            cRoots.Should().HaveCount(1);
            cRoots.Should().Contain(componentC.Id);
            ((IDependencyGraph)dependencyGraph).IsComponentExplicitlyReferenced(componentC.Id).Should().BeTrue();
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_NullComponentId_ArgumentNullExceptionIsThrown()
        {
            Action action = () => dependencyGraph.GetExplicitReferencedDependencyIds(null);
            action.Should().Throw<ArgumentNullException>();

            action = () => dependencyGraph.GetExplicitReferencedDependencyIds(string.Empty);
            action.Should().Throw<ArgumentNullException>();

            action = () => dependencyGraph.GetExplicitReferencedDependencyIds("   ");
            action.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void GetExplicitReferencedDependencyIds_ComponentIdIsNotRegisteredInGraph_ArgumentExceptionIsThrown()
        {
            Action action = () => dependencyGraph.GetExplicitReferencedDependencyIds("nonExistingId");
            action.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void IsDevelopmentDependency_ReturnsAsExpected()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = false };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC);
            dependencyGraph.AddComponent(componentA, componentC.Id);

            dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(true);
            dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
            dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(null);
        }

        [TestMethod]
        public void IsDevelopmentDependency_ReturnsAsExpected_AfterMerge()
        {
            var componentA = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = true };
            var componentB = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = false };
            var componentC = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };

            dependencyGraph.AddComponent(componentA);
            dependencyGraph.AddComponent(componentB, componentA.Id);
            dependencyGraph.AddComponent(componentC);
            dependencyGraph.AddComponent(componentA, componentC.Id);

            var componentANewValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA", IsDevelopmentDependency = false };
            var componentBNewValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB", IsDevelopmentDependency = true };
            var componentCNewValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC", IsDevelopmentDependency = true };
            dependencyGraph.AddComponent(componentANewValue);
            dependencyGraph.AddComponent(componentBNewValue);
            dependencyGraph.AddComponent(componentCNewValue);

            dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(false);
            dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
            dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(true);

            var componentANullValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentA" };
            var componentBNullValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentB" };
            var componentCNullValue = new DependencyGraph.DependencyGraph.ComponentRefNode { Id = "componentC" };
            dependencyGraph.AddComponent(componentANullValue);
            dependencyGraph.AddComponent(componentBNullValue);
            dependencyGraph.AddComponent(componentCNullValue);

            dependencyGraph.IsDevelopmentDependency(componentA.Id).Should().Be(false);
            dependencyGraph.IsDevelopmentDependency(componentB.Id).Should().Be(false);
            dependencyGraph.IsDevelopmentDependency(componentC.Id).Should().Be(true);
        }
    }
}
