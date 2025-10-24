#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#pragma warning disable IDE0001 // Simplify Names
using DependencyGraph = Microsoft.ComponentDetection.Common.DependencyGraph.DependencyGraph;
#pragma warning restore IDE0001 // Simplify Names

[TestClass]
public class ComponentRecorderTests
{
    private ComponentRecorder componentRecorder;

    [TestInitialize]
    public void TestInitialize()
    {
        this.componentRecorder = new ComponentRecorder();
    }

    [TestMethod]
    public void RegisterUsage_RegisterNewDetectedComponent_NodeInTheGraphIsCreated()
    {
        var location = "location";

        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(location);

        var detectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"));
        singleFileComponentRecorder.RegisterUsage(detectedComponent);
        singleFileComponentRecorder.GetComponent(detectedComponent.Component.Id).Should().NotBeNull();

        var dependencyGraph = this.componentRecorder.GetDependencyGraphForLocation(location);

        dependencyGraph.GetDependenciesForComponent(detectedComponent.Component.Id).Should().NotBeNull();
    }

    [TestMethod]
    public void RegisterUsage_NewDetectedComponentHasParent_NewRelationshipIsInserted()
    {
        var location = "location";

        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(location);

        var detectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"));
        var parentComponent = new DetectedComponent(new NpmComponent("test2", "2.0.0"));

        singleFileComponentRecorder.RegisterUsage(parentComponent);
        singleFileComponentRecorder.RegisterUsage(detectedComponent, parentComponentId: parentComponent.Component.Id);

        var dependencyGraph = this.componentRecorder.GetDependencyGraphForLocation(location);

        dependencyGraph.GetDependenciesForComponent(parentComponent.Component.Id).Should().Contain(detectedComponent.Component.Id);
    }

    [TestMethod]
    public void RegisterUsage_DetectedComponentIsNull_ArgumentNullExceptionIsThrown()
    {
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("location");

        Action action = () => singleFileComponentRecorder.RegisterUsage(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void RegisterUsage_DevelopmentDependencyHasValue_componentNodeHasDependencyScope()
    {
        var location = "location";
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(location);
        var detectedComponent = new DetectedComponent(new MavenComponent("org.apache.maven", "maven-artifact", "3.6.1"));

        singleFileComponentRecorder.RegisterUsage(detectedComponent, dependencyScope: DependencyScope.MavenProvided);
        var dependencyGraph = this.componentRecorder.GetDependencyGraphForLocation(location);

        dependencyGraph.GetDependencyScope(detectedComponent.Component.Id).Should().NotBeNull();
        dependencyGraph.GetDependencyScope(detectedComponent.Component.Id).Should().Be(DependencyScope.MavenProvided);
    }

    [TestMethod]
    public void RegisterUsage_DetectedComponentWithNullComponent_ArgumentExceptionIsThrown()
    {
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent = new DetectedComponent(null);

        Action action = () => singleFileComponentRecorder.RegisterUsage(detectedComponent);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void RegisterUsage_DetectedComponentExistAndUpdateFunctionIsNull_NotExceptionIsThrown()
    {
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"));
        singleFileComponentRecorder.RegisterUsage(detectedComponent);

        Action action = () => singleFileComponentRecorder.RegisterUsage(detectedComponent);

        action.Should().NotThrow<Exception>();
    }

    [TestMethod]
    public void CreateComponentsingleFileComponentRecorderForLocation_LocationIsNull_ArgumentNullExceptionIsThrown()
    {
        Action action = () => this.componentRecorder.CreateSingleFileComponentRecorder(null);
        action.Should().Throw<ArgumentNullException>();

        action = () => this.componentRecorder.CreateSingleFileComponentRecorder(string.Empty);
        action.Should().Throw<ArgumentNullException>();

        action = () => this.componentRecorder.CreateSingleFileComponentRecorder("  ");
        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GetComponent_ComponentNotExist_NullIsReturned()
    {
        this.componentRecorder.CreateSingleFileComponentRecorder("someMockLocation").GetComponent("nonexistedcomponentId").Should().BeNull();
    }

    [TestMethod]
    public void GetDetectedComponents_AreComponentsRegistered_ComponentsAreReturned()
    {
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"));
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"));

        singleFileComponentRecorder.RegisterUsage(detectedComponent1);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2);

        var detectedComponents = this.componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(2);
        detectedComponents.Should().Contain(detectedComponent1);
        detectedComponents.Should().Contain(detectedComponent2);
    }

    [TestMethod]
    public void GetDetectedComponents_NoComponentsAreRegistered_EmptyCollectionIsReturned()
    {
        var detectedComponents = this.componentRecorder.GetDetectedComponents();

        detectedComponents.Should().NotBeNull();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public void GetAllDependencyGraphs_ReturnsImmutableDictionaryWithContents()
    {
        // Setup an initial, simple graph.
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("/some/location");

        // We want to take a look at how the class is used by it's friends
        var internalsView = (ComponentRecorder.SingleFileComponentRecorder)singleFileComponentRecorder;
        var graph = internalsView.DependencyGraph;
        var component1 = new DependencyGraph.ComponentRefNode { Id = "component1" };
        graph.AddComponent(component1);
        var component2 = new DependencyGraph.ComponentRefNode { Id = "component2" };
        graph.AddComponent(component2);

        component2.DependedOnByIds.Add(component1.Id);
        component1.DependencyIds.Add(component2.Id);

        // Get readonly content from graph
        var allGraphs = this.componentRecorder.GetDependencyGraphsByLocation();
        var expectedGraph = allGraphs["/some/location"];

        // Verify content looks correct
        expectedGraph.Should().NotBeNull();
        var allComponents = expectedGraph.GetComponents();
        allComponents.Should().Contain(component1.Id);
        allComponents.Should().Contain(component2.Id);

        var component1Deps = expectedGraph.GetDependenciesForComponent(component1.Id);
        component1Deps.Should().Contain(component2.Id);

        var component2Deps = expectedGraph.GetDependenciesForComponent(component2.Id);
        component2Deps.Should().BeEmpty();

        // Verify dictionary is immutable (type enforces, make sure we can't interact with a downcasted dictionary)
        var asDictionary = allGraphs as IDictionary<string, IDependencyGraph>;
        asDictionary.Should().NotBeNull();
        Action attemptedSet = () => asDictionary["/some/location"] = null;
        attemptedSet.Should().Throw<NotSupportedException>();

        Action attemptedClear = () => asDictionary.Clear();
        attemptedClear.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void GetAllDependencyGraphs_ReturnedGraphsAreImmutable()
    {
        // Setup an initial, simple graph.
        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder("/some/location");

        // We want to take a look at how the class is used by it's friends
        var internalsView = (ComponentRecorder.SingleFileComponentRecorder)singleFileComponentRecorder;
        var graph = internalsView.DependencyGraph;

        var component1 = new DependencyGraph.ComponentRefNode { Id = "component1" };
        graph.AddComponent(component1);
        var component2 = new DependencyGraph.ComponentRefNode { Id = "component2" };
        graph.AddComponent(component2);

        component2.DependedOnByIds.Add(component1.Id);
        component1.DependencyIds.Add(component2.Id);

        // Get readonly content from graph
        var allGraphs = this.componentRecorder.GetDependencyGraphsByLocation();
        var expectedGraph = allGraphs["/some/location"];

        // Verify content looks correct
        var component1Deps = expectedGraph.GetDependenciesForComponent(component1.Id);
        component1Deps.Should().NotBeEmpty();

        // Verify componentId set can't be interacted with after downcasting
        var asCollection = component1Deps as ICollection<string>;
        asCollection.Should().NotBeNull();

        Action attemptedClear = () => asCollection.Clear();
        attemptedClear.Should().Throw<NotSupportedException>();

        Action attemptedAdd = () => asCollection.Add("should't work");
        attemptedAdd.Should().Throw<NotSupportedException>();
    }
}
