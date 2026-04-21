#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ReconcileDependencyGraphIdsTests
{
    // Bare Ids: "name version - Npm" (no DownloadUrl)
    // Rich Ids: "name version - Npm [DownloadUrl:url]" (with DownloadUrl)
    // These must match what NpmComponent actually produces.
    [TestMethod]
    public void ReconcileDependencyGraphIds_BareAndRichNodes_MergesBaresIntoRich()
    {
        // Arrange: Paul's scenario — two detectors scan the same location, producing bare + rich nodes
        var rootBare = MakeBare("root", "1.0.0");
        var rootRich = MakeRich("root", "1.0.0", "https://npmjs.org/pkg/root");
        var t1Bare = MakeBare("transient1", "1.0.0");
        var t1Rich = MakeRich("transient1", "1.0.0", "https://npmjs.org/pkg/transient1");
        var t2Bare = MakeBare("transient2", "2.0.0");
        var t2Rich = MakeRich("transient2", "2.0.0", "https://npmjs.org/pkg/transient2");

        var graph = new DependencyGraph
        {
            { rootBare.Id, [t1Bare.Id, t2Bare.Id] },
            { t1Bare.Id, null },
            { t2Bare.Id, null },
            { rootRich.Id, [t1Rich.Id, t2Rich.Id] },
            { t1Rich.Id, null },
            { t2Rich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/project/package-lock.json", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [rootBare.Id, rootRich.Id],
                    DevelopmentDependencies = [t1Bare.Id],
                    Dependencies = [rootBare.Id, t1Bare.Id, t2Bare.Id, rootRich.Id, t1Rich.Id, t2Rich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(rootRich, t1Rich, t2Rich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert
        var result = graphs["/project/package-lock.json"];
        var resultGraph = result.Graph;

        // Bare nodes should be gone
        resultGraph.Should().NotContainKey(rootBare.Id);
        resultGraph.Should().NotContainKey(t1Bare.Id);
        resultGraph.Should().NotContainKey(t2Bare.Id);

        // Rich nodes should remain with merged edges
        resultGraph.Should().ContainKey(rootRich.Id);
        resultGraph[rootRich.Id].Should().Contain(t1Rich.Id);
        resultGraph[rootRich.Id].Should().Contain(t2Rich.Id);

        resultGraph.Should().ContainKey(t1Rich.Id);
        resultGraph.Should().ContainKey(t2Rich.Id);

        // Metadata sets should only contain rich Ids
        result.ExplicitlyReferencedComponentIds.Should().Contain(rootRich.Id);
        result.ExplicitlyReferencedComponentIds.Should().NotContain(rootBare.Id);

        result.DevelopmentDependencies.Should().Contain(t1Rich.Id);
        result.DevelopmentDependencies.Should().NotContain(t1Bare.Id);

        result.Dependencies.Should().NotContain(rootBare.Id);
        result.Dependencies.Should().NotContain(t1Bare.Id);
        result.Dependencies.Should().NotContain(t2Bare.Id);
        result.Dependencies.Should().Contain(rootRich.Id);
        result.Dependencies.Should().Contain(t1Rich.Id);
        result.Dependencies.Should().Contain(t2Rich.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_MultipleRichVariants_BareNodeMergesIntoAll()
    {
        // Arrange: bare A maps to rich A[url1] and rich A[url2]
        var aBare = MakeBare("pkgA", "1.0.0");
        var aRich1 = MakeRich("pkgA", "1.0.0", "https://registry1.com/pkgA");
        var aRich2 = MakeRich("pkgA", "1.0.0", "https://registry2.com/pkgA");
        var bBare = MakeBare("pkgB", "1.0.0");
        var bRich = MakeRich("pkgB", "1.0.0", "https://registry1.com/pkgB");

        var graph = new DependencyGraph
        {
            { aBare.Id, [bBare.Id] },
            { bBare.Id, null },
            { aRich1.Id, [bRich.Id] },
            { aRich2.Id, null },
            { bRich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [aBare.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [aBare.Id, bBare.Id, aRich1.Id, aRich2.Id, bRich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(aRich1, aRich2, bRich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert
        var result = graphs["/loc"].Graph;
        result.Should().NotContainKey(aBare.Id);
        result.Should().NotContainKey(bBare.Id);

        // Both rich A variants should get the merged edge (bBare → bRich)
        result[aRich1.Id].Should().Contain(bRich.Id);
        result[aRich2.Id].Should().Contain(bRich.Id);

        // Explicit set should contain both rich variants
        graphs["/loc"].ExplicitlyReferencedComponentIds.Should().Contain(aRich1.Id);
        graphs["/loc"].ExplicitlyReferencedComponentIds.Should().Contain(aRich2.Id);
        graphs["/loc"].ExplicitlyReferencedComponentIds.Should().NotContain(aBare.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_BareOnlyNodes_LeftUnchanged()
    {
        // Arrange: bare-only components (no rich counterpart anywhere)
        var rootBare = MakeBare("root", "1.0.0");
        var t1Bare = MakeBare("transient1", "1.0.0");

        var graph = new DependencyGraph
        {
            { rootBare.Id, [t1Bare.Id] },
            { t1Bare.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [rootBare.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [rootBare.Id, t1Bare.Id],
                }
            },
        };

        // mergedComponents only has bare entries (Id == BaseId)
        var mergedComponents = MakeDetectedComponents(rootBare, t1Bare);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert: nothing should change
        var result = graphs["/loc"];
        result.Graph.Should().ContainKey(rootBare.Id);
        result.Graph.Should().ContainKey(t1Bare.Id);
        result.Graph[rootBare.Id].Should().Contain(t1Bare.Id);
        result.ExplicitlyReferencedComponentIds.Should().Contain(rootBare.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_RichOnlyNodes_LeftUnchanged()
    {
        // Arrange: all nodes are rich (no bare counterparts in the graph)
        var rootRich = MakeRich("root", "1.0.0", "https://npmjs.org/pkg/root");
        var t1Rich = MakeRich("transient1", "1.0.0", "https://npmjs.org/pkg/transient1");

        var graph = new DependencyGraph
        {
            { rootRich.Id, [t1Rich.Id] },
            { t1Rich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [rootRich.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [rootRich.Id, t1Rich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(rootRich, t1Rich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert: nothing should change
        var result = graphs["/loc"];
        result.Graph.Should().ContainKey(rootRich.Id);
        result.Graph.Should().ContainKey(t1Rich.Id);
        result.Graph[rootRich.Id].Should().Contain(t1Rich.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_InboundEdgesRewritten()
    {
        // Arrange: non-bare node has an edge pointing to a bare node
        var parentRich = MakeRich("parent", "1.0.0", "https://npmjs.org/pkg/parent");
        var t1Bare = MakeBare("transient1", "1.0.0");
        var t1Rich = MakeRich("transient1", "1.0.0", "https://npmjs.org/pkg/transient1");

        var graph = new DependencyGraph
        {
            { parentRich.Id, [t1Bare.Id] },
            { t1Bare.Id, null },
            { t1Rich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [parentRich.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [parentRich.Id, t1Bare.Id, t1Rich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(parentRich, t1Rich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert: parentRich's edge should now point to t1Rich
        var result = graphs["/loc"].Graph;
        result[parentRich.Id].Should().Contain(t1Rich.Id);
        result[parentRich.Id].Should().NotContain(t1Bare.Id);
        result.Should().NotContainKey(t1Bare.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_SelfEdgesNotIntroduced()
    {
        // Arrange: bare A → bare A (self-cycle), with rich A in the graph
        var aBare = MakeBare("pkgA", "1.0.0");
        var aRich = MakeRich("pkgA", "1.0.0", "https://npmjs.org/pkg/pkgA");

        var graph = new DependencyGraph
        {
            { aBare.Id, [aBare.Id] },
            { aRich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [],
                    DevelopmentDependencies = [],
                    Dependencies = [aBare.Id, aRich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(aRich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert: aRich should not have a self-edge
        var result = graphs["/loc"].Graph;
        result.Should().NotContainKey(aBare.Id);
        result.Should().ContainKey(aRich.Id);
        result[aRich.Id]?.Should().NotContain(aRich.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_LeafPreservation_BareLeafRichNonLeaf()
    {
        // Arrange: bare T1 is leaf, rich T1 has edges — merging should preserve rich edges
        var rootBare = MakeBare("root", "1.0.0");
        var rootRich = MakeRich("root", "1.0.0", "https://npmjs.org/pkg/root");
        var t1Bare = MakeBare("transient1", "1.0.0");
        var t1Rich = MakeRich("transient1", "1.0.0", "https://npmjs.org/pkg/transient1");
        var t2Rich = MakeRich("transient2", "2.0.0", "https://npmjs.org/pkg/transient2");

        var graph = new DependencyGraph
        {
            { rootBare.Id, [t1Bare.Id] },
            { t1Bare.Id, null },
            { rootRich.Id, [t1Rich.Id] },
            { t1Rich.Id, [t2Rich.Id] },
            { t2Rich.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc", new DependencyGraphWithMetadata
                {
                    Graph = graph,
                    ExplicitlyReferencedComponentIds = [],
                    DevelopmentDependencies = [],
                    Dependencies = [rootBare.Id, t1Bare.Id, rootRich.Id, t1Rich.Id, t2Rich.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(rootRich, t1Rich, t2Rich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert: rich T1 should keep its original edges (T2)
        var result = graphs["/loc"].Graph;
        result[t1Rich.Id].Should().Contain(t2Rich.Id);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_NullGraphCollection_NoOp()
    {
        // Act/Assert: should not throw
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(null, []);
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_EmptyGraphCollection_NoOp()
    {
        var graphs = new DependencyGraphCollection();
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, []);
        graphs.Should().BeEmpty();
    }

    [TestMethod]
    public void ReconcileDependencyGraphIds_MultipleLocations_ReconcileEachIndependently()
    {
        // Arrange: location 1 has bare+rich, location 2 has bare-only
        var rootBare = MakeBare("root", "1.0.0");
        var rootRich = MakeRich("root", "1.0.0", "https://npmjs.org/pkg/root");

        var graph1 = new DependencyGraph
        {
            { rootBare.Id, null },
            { rootRich.Id, null },
        };

        var graph2 = new DependencyGraph
        {
            { rootBare.Id, null },
        };

        var graphs = new DependencyGraphCollection
        {
            {
                "/loc1", new DependencyGraphWithMetadata
                {
                    Graph = graph1,
                    ExplicitlyReferencedComponentIds = [rootBare.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [rootBare.Id, rootRich.Id],
                }
            },
            {
                "/loc2", new DependencyGraphWithMetadata
                {
                    Graph = graph2,
                    ExplicitlyReferencedComponentIds = [rootBare.Id],
                    DevelopmentDependencies = [],
                    Dependencies = [rootBare.Id],
                }
            },
        };

        var mergedComponents = MakeDetectedComponents(rootRich);

        // Act
        DefaultGraphTranslationService.ReconcileDependencyGraphIds(graphs, mergedComponents);

        // Assert
        // Location 1: bare removed, rich kept
        graphs["/loc1"].Graph.Should().NotContainKey(rootBare.Id);
        graphs["/loc1"].Graph.Should().ContainKey(rootRich.Id);
        graphs["/loc1"].ExplicitlyReferencedComponentIds.Should().Contain(rootRich.Id);
        graphs["/loc1"].ExplicitlyReferencedComponentIds.Should().NotContain(rootBare.Id);

        // Location 2: bare kept (no rich counterpart in this graph)
        graphs["/loc2"].Graph.Should().ContainKey(rootBare.Id);
    }

    /// <summary>Creates a bare NpmComponent (no DownloadUrl, so Id == BaseId).</summary>
    private static NpmComponent MakeBare(string name, string version) => new(name, version);

    /// <summary>Creates a rich NpmComponent (with DownloadUrl, so Id != BaseId).</summary>
    private static NpmComponent MakeRich(string name, string version, string downloadUrl)
    {
        var c = new NpmComponent(name, version) { DownloadUrl = new Uri(downloadUrl) };
        return c;
    }

    /// <summary>Wraps TypedComponents into DetectedComponent list (as mergedComponents would be).</summary>
    private static List<DetectedComponent> MakeDetectedComponents(params TypedComponent[] components)
    {
        var result = new List<DetectedComponent>();
        foreach (var c in components)
        {
            result.Add(new DetectedComponent(c));
        }

        return result;
    }
}
