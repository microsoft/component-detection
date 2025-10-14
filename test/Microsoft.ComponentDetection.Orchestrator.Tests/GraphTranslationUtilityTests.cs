#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests;

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GraphTranslationUtilityTests
{
    [TestMethod]
    public void Test_AccumulateAndConvertToContract()
    {
        var componentRecorder = new ComponentRecorder();

        var file1 = componentRecorder.CreateSingleFileComponentRecorder("file1.json");
        file1.RegisterUsage(new DetectedComponent(new NpmComponent("webpack", "1.0.0")));

        var file2 = componentRecorder.CreateSingleFileComponentRecorder("file2.json");
        file2.RegisterUsage(new DetectedComponent(new NpmComponent("webpack", "2.0.0")), isExplicitReferencedDependency: true);

        var dependencyGraphs = new List<IReadOnlyDictionary<string, IDependencyGraph>>() { componentRecorder.GetDependencyGraphsByLocation() };

        var convertedGraphContract = GraphTranslationUtility.AccumulateAndConvertToContract(dependencyGraphs);

        convertedGraphContract.Should().HaveCount(2);
        convertedGraphContract.Keys.Should().BeEquivalentTo(new List<string>() { "file1.json", "file2.json" });

        var graph1 = convertedGraphContract["file1.json"];
        graph1.ExplicitlyReferencedComponentIds.Should().BeEmpty();
        graph1.Graph.Keys.Should().BeEquivalentTo(new List<string>() { "webpack 1.0.0 - Npm" });
        graph1.DevelopmentDependencies.Should().BeEmpty();

        var graph2 = convertedGraphContract["file2.json"];
        graph2.ExplicitlyReferencedComponentIds.Should().BeEquivalentTo(new List<string>() { "webpack 2.0.0 - Npm" });
        graph2.Graph.Keys.Should().BeEquivalentTo(new List<string>() { "webpack 2.0.0 - Npm" });
        graph2.DevelopmentDependencies.Should().BeEmpty();
    }
}
