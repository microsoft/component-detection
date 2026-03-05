#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
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
    public void GetDetectedComponents_SameComponentAcrossFiles_MergesLicensesConcluded()
    {
        var recorder1 = this.componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        var recorder2 = this.componentRecorder.CreateSingleFileComponentRecorder("file2.json");

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT", "Apache-2.0"],
        };

        recorder1.RegisterUsage(component1);
        recorder2.RegisterUsage(component2);

        var result = this.componentRecorder.GetDetectedComponents().Single();
        result.LicensesConcluded.Should().HaveCount(2);
        result.LicensesConcluded.Should().Contain("MIT");
        result.LicensesConcluded.Should().Contain("Apache-2.0");
    }

    [TestMethod]
    public void GetDetectedComponents_SameComponentAcrossFiles_MergesSuppliers()
    {
        var recorder1 = this.componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        var recorder2 = this.componentRecorder.CreateSingleFileComponentRecorder("file2.json");

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            Suppliers = [new ActorInfo { Name = "Contoso", Type = "Organization" }],
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            Suppliers = [new ActorInfo { Name = "Contoso", Type = "Organization" }, new ActorInfo { Name = "Fabrikam", Type = "Organization" }],
        };

        recorder1.RegisterUsage(component1);
        recorder2.RegisterUsage(component2);

        var result = this.componentRecorder.GetDetectedComponents().Single();

        // Exact duplicate "Contoso"/"Organization" is deduped; "Fabrikam" is kept
        result.Suppliers.Should().HaveCount(2);
        result.Suppliers.Should().Contain(s => s.Name == "Contoso");
        result.Suppliers.Should().Contain(s => s.Name == "Fabrikam");
    }

    [TestMethod]
    public void GetDetectedComponents_SameComponentAcrossFiles_OneHasNullFields_PreservesNonNull()
    {
        var recorder1 = this.componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        var recorder2 = this.componentRecorder.CreateSingleFileComponentRecorder("file2.json");

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"));

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
            Suppliers = [new ActorInfo { Name = "Contoso", Type = "Organization" }],
        };

        recorder1.RegisterUsage(component1);
        recorder2.RegisterUsage(component2);

        var result = this.componentRecorder.GetDetectedComponents().Single();
        result.LicensesConcluded.Should().ContainSingle().Which.Should().Be("MIT");
        result.Suppliers.Should().ContainSingle().Which.Name.Should().Be("Contoso");
    }

    [TestMethod]
    public void GetDetectedComponents_SameComponentAcrossThreeFiles_MergesAllFields()
    {
        var recorder1 = this.componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        var recorder2 = this.componentRecorder.CreateSingleFileComponentRecorder("file2.json");
        var recorder3 = this.componentRecorder.CreateSingleFileComponentRecorder("file3.json");

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
            Suppliers = [new ActorInfo { Name = "Alice", Type = "Person" }],
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["Apache-2.0"],
            Suppliers = [new ActorInfo { Name = "Bob", Type = "Person" }],
        };

        var component3 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT", "GPL-3.0"],
            Suppliers = [new ActorInfo { Name = "Alice", Type = "Person" }, new ActorInfo { Name = "Contoso", Type = "Organization" }],
        };

        recorder1.RegisterUsage(component1);
        recorder2.RegisterUsage(component2);
        recorder3.RegisterUsage(component3);

        var result = this.componentRecorder.GetDetectedComponents().Single();

        result.LicensesConcluded.Should().HaveCount(3);
        result.LicensesConcluded.Should().Contain("MIT");
        result.LicensesConcluded.Should().Contain("Apache-2.0");
        result.LicensesConcluded.Should().Contain("GPL-3.0");

        // "Alice"/"Person" appears in file1 and file3 — should be deduped
        result.Suppliers.Should().HaveCount(3);
        result.Suppliers.Should().Contain(s => s.Name == "Alice");
        result.Suppliers.Should().Contain(s => s.Name == "Bob");
        result.Suppliers.Should().Contain(s => s.Name == "Contoso");
    }

    [TestMethod]
    public void GetDetectedComponents_DifferentComponentsAcrossFiles_FieldsNotMixed()
    {
        var recorder1 = this.componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        var recorder2 = this.componentRecorder.CreateSingleFileComponentRecorder("file2.json");

        var componentA = new DetectedComponent(new NpmComponent("pkg-a", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
            Suppliers = [new ActorInfo { Name = "Alice", Type = "Person" }],
        };

        var componentB = new DetectedComponent(new NpmComponent("pkg-b", "2.0.0"))
        {
            LicensesConcluded = ["GPL-3.0"],
            Suppliers = [new ActorInfo { Name = "Bob", Type = "Person" }],
        };

        recorder1.RegisterUsage(componentA);
        recorder2.RegisterUsage(componentB);

        var results = this.componentRecorder.GetDetectedComponents().ToList();
        results.Should().HaveCount(2);

        var resultA = results.Single(c => ((NpmComponent)c.Component).Name == "pkg-a");
        resultA.LicensesConcluded.Should().BeEquivalentTo(["MIT"]);
        resultA.Suppliers.Should().ContainSingle().Which.Name.Should().Be("Alice");

        var resultB = results.Single(c => ((NpmComponent)c.Component).Name == "pkg-b");
        resultB.LicensesConcluded.Should().BeEquivalentTo(["GPL-3.0"]);
        resultB.Suppliers.Should().ContainSingle().Which.Name.Should().Be("Bob");
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
