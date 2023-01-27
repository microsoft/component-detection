namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

[TestClass]
public class PipComponentDetectorTests
{
    private Mock<IPythonCommandService> pythonCommandService;
    private Mock<IPythonResolver> pythonResolver;
    private Mock<ILogger> loggerMock;

    private DetectorTestUtility<PipComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        this.pythonCommandService = new Mock<IPythonCommandService>();
        this.pythonResolver = new Mock<IPythonResolver>();
        this.loggerMock = new Mock<ILogger>();

        var detector = new PipComponentDetector
        {
            PythonCommandService = this.pythonCommandService.Object,
            PythonResolver = this.pythonResolver.Object,
            Logger = this.loggerMock.Object,
        };

        this.detectorTestUtility = DetectorTestUtilityCreator.Create<PipComponentDetector>()
            .WithDetector(detector);
    }

    [TestMethod]
    public async Task TestPipDetector_PythonNotInstalledAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        this.loggerMock.Setup(x => x.LogInfo(It.Is<string>(l => l.Contains("No python found"))));

        var (result, componentRecorder) = await this.detectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithLogger(this.loggerMock)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        this.loggerMock.VerifyAll();
    }

    [TestMethod]
    public async Task TestPipDetector_PythonInstalledNoFilesAsync()
    {
        var (result, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
    }

    [TestMethod]
    public async Task TestPipDetector_SetupPyAndRequirementsTxtAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var baseSetupPyDependencies = ToGitTuple(new List<string> { "a==1.0", "b>=2.0,!=2.1", "c!=1.1" });
        var baseRequirementsTextDependencies = ToGitTuple(new List<string> { "d~=1.0", "e<=2.0", "f===1.1" });
        baseRequirementsTextDependencies.Add((null, new GitComponent(new Uri("https://github.com/example/example"), "deadbee")));

        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "setup.py"), null)).ReturnsAsync(baseSetupPyDependencies);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies);

        var setupPyRoots = new List<PipGraphNode>
        {
            new PipGraphNode(new PipComponent("a", "1.0")),
            new PipGraphNode(new PipComponent("b", "2.3")),
            new PipGraphNode(new PipComponent("c", "1.0.1")),
        };

        setupPyRoots[1].Children.Add(new PipGraphNode(new PipComponent("z", "1.2.3")));

        var requirementsTxtRoots = new List<PipGraphNode>
        {
            new PipGraphNode(new PipComponent("d", "1.0")),
            new PipGraphNode(new PipComponent("e", "1.9")),
            new PipGraphNode(new PipComponent("f", "1.1")),
        };

        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "b")))).ReturnsAsync(setupPyRoots);
        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "d")))).ReturnsAsync(requirementsTxtRoots);

        var (result, componentRecorder) = await this.detectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(8, detectedComponents.Count());

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();
        Assert.AreEqual("1.2.3", ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "z").Component).Version);

        foreach (var item in setupPyRoots)
        {
            var reference = item.Value;

            Assert.AreEqual(reference.Version, ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == reference.Name).Component).Version);
        }

        var gitComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Type == ComponentType.Git);
        gitComponents.Count().Should().Be(1);
        var gitComponent = (GitComponent)gitComponents.Single().Component;

        gitComponent.RepositoryUrl.Should().Be("https://github.com/example/example");
        gitComponent.CommitHash.Should().Be("deadbee");
    }

    [TestMethod]
    public async Task TestPipDetector_ComponentsDedupedAcrossFilesAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var baseRequirementsTextDependencies = ToGitTuple(new List<string> { "d~=1.0", "e<=2.0", "f===1.1", "h==1.3" });
        var baseRequirementsTextDependencies2 = ToGitTuple(new List<string> { "D~=1.0", "E<=2.0", "F===1.1", "g==2" });
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "TEST", "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies2);

        var requirementsTxtRoots = new List<PipGraphNode>
        {
            new PipGraphNode(new PipComponent("d", "1.0")),
            new PipGraphNode(new PipComponent("e", "1.9")),
            new PipGraphNode(new PipComponent("f", "1.1")),
            new PipGraphNode(new PipComponent("h", "1.3")),
        };
        var requirementsTxtRoots2 = new List<PipGraphNode>
        {
            new PipGraphNode(new PipComponent("D", "1.0")),
            new PipGraphNode(new PipComponent("E", "1.9")),
            new PipGraphNode(new PipComponent("F", "1.1")),
            new PipGraphNode(new PipComponent("g", "1.2")),
        };

        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "h")))).ReturnsAsync(requirementsTxtRoots);
        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "g")))).ReturnsAsync(requirementsTxtRoots2);

        var (result, componentRecorder) = await this.detectorTestUtility
            .WithFile("requirements.txt", string.Empty)
            .WithFile("requirements.txt", string.Empty, fileLocation: Path.Join(Path.GetTempPath(), "TEST", "requirements.txt"))
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        Assert.AreEqual(5, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestPipDetector_ComponentRecorderAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        const string file1 = "c:\\repo\\setup.py";
        const string file2 = "c:\\repo\\lib\\requirements.txt";

        var baseReqs = ToGitTuple(new List<string> { "a~=1.0", "b<=2.0", });
        var altReqs = ToGitTuple(new List<string> { "c~=1.0", "d<=2.0", "e===1.1" });
        this.pythonCommandService.Setup(x => x.ParseFileAsync(file1, null)).ReturnsAsync(baseReqs);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(file2, null)).ReturnsAsync(altReqs);

        var rootA = new PipGraphNode(new PipComponent("a", "1.0"));
        var rootB = new PipGraphNode(new PipComponent("b", "2.1"));
        var rootC = new PipGraphNode(new PipComponent("c", "1.0"));
        var rootD = new PipGraphNode(new PipComponent("d", "1.9"));
        var rootE = new PipGraphNode(new PipComponent("e", "1.1"));

        var red = new PipGraphNode(new PipComponent("red", "0.2"));
        var green = new PipGraphNode(new PipComponent("green", "1.3"));
        var blue = new PipGraphNode(new PipComponent("blue", "0.4"));

        var cat = new PipGraphNode(new PipComponent("cat", "1.8"));
        var lion = new PipGraphNode(new PipComponent("lion", "3.8"));
        var dog = new PipGraphNode(new PipComponent("dog", "2.1"));

        rootA.Children.Add(red);
        rootB.Children.Add(green);
        rootC.Children.AddRange(new[] { red, blue, });
        rootD.Children.Add(cat);
        green.Children.Add(cat);
        cat.Children.Add(lion);
        blue.Children.Add(cat);
        blue.Children.Add(dog);

        this.pythonResolver.Setup(x =>
                x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "a"))))
            .ReturnsAsync(new List<PipGraphNode> { rootA, rootB, });

        this.pythonResolver.Setup(x =>
                x.ResolveRootsAsync(It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "c"))))
            .ReturnsAsync(new List<PipGraphNode> { rootC, rootD, rootE, });

        var (result, componentRecorder) = await this.detectorTestUtility
            .WithFile("setup.py", string.Empty, fileLocation: file1)
            .WithFile("setup.py", string.Empty, fileLocation: file2)
            .ExecuteDetectorAsync();

        var discoveredComponents = componentRecorder.GetDetectedComponents();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        Assert.AreEqual(11, discoveredComponents.Count());

        var rootIds = new[]
        {
            "a 1.0 - pip",
            "b 2.1 - pip",
            "c 1.0 - pip",
            "d 1.9 - pip",
            "e 1.1 - pip",
        };

        foreach (var rootId in rootIds)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<PipComponent>(
                rootId,
                x => x.Id == rootId);
        }

        CheckChild(componentRecorder, "red 0.2 - pip", new[] { "a 1.0 - pip", "c 1.0 - pip", });
        CheckChild(componentRecorder, "green 1.3 - pip", new[] { "b 2.1 - pip", });
        CheckChild(componentRecorder, "blue 0.4 - pip", new[] { "c 1.0 - pip", });
        CheckChild(componentRecorder, "cat 1.8 - pip", new[] { "b 2.1 - pip", "c 1.0 - pip", "d 1.9 - pip", });
        CheckChild(componentRecorder, "lion 3.8 - pip", new[] { "b 2.1 - pip", "c 1.0 - pip", "d 1.9 - pip", });
        CheckChild(componentRecorder, "dog 2.1 - pip", new[] { "c 1.0 - pip", });

        var graphsByLocations = componentRecorder.GetDependencyGraphsByLocation();
        Assert.AreEqual(2, graphsByLocations.Count);

        var graph1ComponentsWithDeps = new Dictionary<string, string[]>
        {
            { "a 1.0 - pip", new[] { "red 0.2 - pip" } },
            { "b 2.1 - pip", new[] { "green 1.3 - pip" } },
            { "red 0.2 - pip", Array.Empty<string>() },
            { "green 1.3 - pip", new[] { "cat 1.8 - pip" } },
            { "cat 1.8 - pip", new[] { "lion 3.8 - pip" } },
            { "lion 3.8 - pip", Array.Empty<string>() },
        };

        var graph1 = graphsByLocations[file1];
        Assert.IsTrue(graph1ComponentsWithDeps.Keys.Take(2).All(graph1.IsComponentExplicitlyReferenced));
        Assert.IsTrue(graph1ComponentsWithDeps.Keys.Skip(2).All(a => !graph1.IsComponentExplicitlyReferenced(a)));
        CheckGraphStructure(graph1, graph1ComponentsWithDeps);

        var graph2ComponentsWithDeps = new Dictionary<string, string[]>
        {
            { "c 1.0 - pip", new[] { "red 0.2 - pip", "blue 0.4 - pip" } },
            { "d 1.9 - pip", new[] { "cat 1.8 - pip" } },
            { "e 1.1 - pip", Array.Empty<string>() },
            { "red 0.2 - pip", Array.Empty<string>() },
            { "blue 0.4 - pip", new[] { "cat 1.8 - pip", "dog 2.1 - pip" } },
            { "cat 1.8 - pip", new[] { "lion 3.8 - pip" } },
            { "dog 2.1 - pip", Array.Empty<string>() },
            { "lion 3.8 - pip", Array.Empty<string>() },
        };

        var graph2 = graphsByLocations[file2];
        Assert.IsTrue(graph2ComponentsWithDeps.Keys.Take(3).All(graph2.IsComponentExplicitlyReferenced));
        Assert.IsTrue(graph2ComponentsWithDeps.Keys.Skip(3).All(a => !graph2.IsComponentExplicitlyReferenced(a)));
        CheckGraphStructure(graph2, graph2ComponentsWithDeps);
    }

    private static void CheckGraphStructure(IDependencyGraph graph, Dictionary<string, string[]> graphComponentsWithDeps)
    {
        var graphComponents = graph.GetComponents().ToArray();
        Assert.AreEqual(
            graphComponentsWithDeps.Keys.Count,
            graphComponents.Length,
            $"Expected {graphComponentsWithDeps.Keys.Count} component to be recorded but got {graphComponents.Length} instead!");

        foreach (var componentId in graphComponentsWithDeps.Keys)
        {
            Assert.IsTrue(
                graphComponents.Contains(componentId),
                $"Component `{componentId}` not recorded!");

            var recordedDeps = graph.GetDependenciesForComponent(componentId).ToArray();
            var expectedDeps = graphComponentsWithDeps[componentId];

            Assert.AreEqual(
                expectedDeps.Length,
                recordedDeps.Length,
                $"Count missmatch of expected dependencies ({JsonConvert.SerializeObject(expectedDeps)}) and recorded dependencies ({JsonConvert.SerializeObject(recordedDeps)}) for `{componentId}`!");

            foreach (var expectedDep in expectedDeps)
            {
                Assert.IsTrue(
                    recordedDeps.Contains(expectedDep),
                    $"Expected `{expectedDep}` in the list of dependencies for `{componentId}` but only recorded: {JsonConvert.SerializeObject(recordedDeps)}");
            }
        }
    }

    private static void CheckChild(IComponentRecorder recorder, string childId, string[] parentIds) => recorder.AssertAllExplicitlyReferencedComponents<PipComponent>(
            childId,
            parentIds.Select(parentId => new Func<PipComponent, bool>(x => x.Id == parentId)).ToArray());

    private static List<(string PackageString, GitComponent Component)> ToGitTuple(IList<string> components) => components.Select<string, (string, GitComponent)>(dep => (dep, null)).ToList();
}
