#nullable disable
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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

[TestClass]
public class SimplePipComponentDetectorTests : BaseDetectorTest<SimplePipComponentDetector>
{
    private readonly Mock<IPythonCommandService> pythonCommandService;
    private readonly Mock<ISimplePythonResolver> pythonResolver;
    private readonly Mock<ILogger<SimplePipComponentDetector>> mockLogger;

    public SimplePipComponentDetectorTests()
    {
        this.pythonCommandService = new Mock<IPythonCommandService>();
        this.DetectorTestUtility.AddServiceMock(this.pythonCommandService);

        this.pythonResolver = new Mock<ISimplePythonResolver>();
        this.DetectorTestUtility.AddServiceMock(this.pythonResolver);

        this.mockLogger = new Mock<ILogger<SimplePipComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);
    }

    [TestMethod]
    public async Task TestPipDetector_PythonNotInstalledAsync()
    {
        this.mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        this.DetectorTestUtility.AddServiceMock(this.mockLogger);

        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.mockLogger.VerifyAll();
    }

    [TestMethod]
    public async Task TestPipDetector_PythonInstalledNoFilesAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task TestPipDetector_SetupPyAndRequirementsTxtAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var baseSetupPyDependencies = this.ToGitTuple(["a==1.0", "b>=2.0,!=2.1", "c!=1.1"]);
        var baseRequirementsTextDependencies = this.ToGitTuple(["d~=1.0", "e<=2.0", "f===1.1"]);
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

        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "b")))).ReturnsAsync(setupPyRoots);
        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "d")))).ReturnsAsync(requirementsTxtRoots);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(8);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "z").Component).Version.Should().Be("1.2.3");

        foreach (var item in setupPyRoots)
        {
            var reference = item.Value;

            ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == reference.Name).Component).Version.Should().Be(reference.Version);
        }

        var gitComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Type == ComponentType.Git);
        gitComponents.Should().ContainSingle();
        var gitComponent = (GitComponent)gitComponents.Single().Component;

        gitComponent.RepositoryUrl.Should().Be("https://github.com/example/example");
        gitComponent.CommitHash.Should().Be("deadbee");
    }

    [TestMethod]
    public async Task TestPipDetector_ComponentsDedupedAcrossFilesAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var baseRequirementsTextDependencies = this.ToGitTuple(["d~=1.0", "e<=2.0", "f===1.1", "h==1.3"]);
        var baseRequirementsTextDependencies2 = this.ToGitTuple(["D~=1.0", "E<=2.0", "F===1.1", "g==2"]);
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

        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "h")))).ReturnsAsync(requirementsTxtRoots);
        this.pythonResolver.Setup(x => x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "g")))).ReturnsAsync(requirementsTxtRoots2);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty)
            .WithFile("requirements.txt", string.Empty, fileLocation: Path.Join(Path.GetTempPath(), "TEST", "requirements.txt"))
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(5);
    }

    [TestMethod]
    public async Task TestPipDetector_ComponentRecorderAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        const string file1 = "c:\\repo\\setup.py";
        const string file2 = "c:\\repo\\lib\\requirements.txt";

        var baseReqs = this.ToGitTuple(["a~=1.0", "b<=2.0",]);
        var altReqs = this.ToGitTuple(["c~=1.0", "d<=2.0", "e===1.1"]);
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
        rootC.Children.AddRange([red, blue,]);
        rootD.Children.Add(cat);
        green.Children.Add(cat);
        cat.Children.Add(lion);
        blue.Children.Add(cat);
        blue.Children.Add(dog);

        this.pythonResolver.Setup(x =>
                x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "a"))))
            .ReturnsAsync([rootA, rootB,]);

        this.pythonResolver.Setup(x =>
                x.ResolveRootsAsync(It.IsAny<ISingleFileComponentRecorder>(), It.Is<IList<PipDependencySpecification>>(p => p.Any(d => d.Name == "c"))))
            .ReturnsAsync([rootC, rootD, rootE,]);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty, fileLocation: file1)
            .WithFile("setup.py", string.Empty, fileLocation: file2)
            .ExecuteDetectorAsync();

        var discoveredComponents = componentRecorder.GetDetectedComponents();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        discoveredComponents.Should().HaveCount(11);

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

        this.CheckChild(componentRecorder, "red 0.2 - pip", ["a 1.0 - pip", "c 1.0 - pip",]);
        this.CheckChild(componentRecorder, "green 1.3 - pip", ["b 2.1 - pip",]);
        this.CheckChild(componentRecorder, "blue 0.4 - pip", ["c 1.0 - pip",]);
        this.CheckChild(componentRecorder, "cat 1.8 - pip", ["b 2.1 - pip", "c 1.0 - pip", "d 1.9 - pip",]);
        this.CheckChild(componentRecorder, "lion 3.8 - pip", ["b 2.1 - pip", "c 1.0 - pip", "d 1.9 - pip",]);
        this.CheckChild(componentRecorder, "dog 2.1 - pip", ["c 1.0 - pip",]);

        var graphsByLocations = componentRecorder.GetDependencyGraphsByLocation();
        graphsByLocations.Should().HaveCount(2);

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
        graph1ComponentsWithDeps.Keys.Take(2).All(graph1.IsComponentExplicitlyReferenced).Should().BeTrue();
        graph1ComponentsWithDeps.Keys.Skip(2).Should().OnlyContain(a => !graph1.IsComponentExplicitlyReferenced(a));
        this.CheckGraphStructure(graph1, graph1ComponentsWithDeps);

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
        graph2ComponentsWithDeps.Keys.Take(3).All(graph2.IsComponentExplicitlyReferenced).Should().BeTrue();
        graph2ComponentsWithDeps.Keys.Skip(3).Should().OnlyContain(a => !graph2.IsComponentExplicitlyReferenced(a));
        this.CheckGraphStructure(graph2, graph2ComponentsWithDeps);
    }

    private void CheckGraphStructure(IDependencyGraph graph, Dictionary<string, string[]> graphComponentsWithDeps)
    {
        var graphComponents = graph.GetComponents().ToArray();
        graphComponents.Should().HaveCount(
            graphComponentsWithDeps.Keys.Count,
            $"Expected {graphComponentsWithDeps.Keys.Count} component to be recorded but got {graphComponents.Length} instead!");

        foreach (var componentId in graphComponentsWithDeps.Keys)
        {
            graphComponents.Should().Contain(componentId, $"Component `{componentId}` not recorded!");

            var recordedDeps = graph.GetDependenciesForComponent(componentId).ToArray();
            var expectedDeps = graphComponentsWithDeps[componentId];

            recordedDeps.Should().HaveCount(
                expectedDeps.Length,
                $"Count missmatch of expected dependencies ({JsonConvert.SerializeObject(expectedDeps)}) and recorded dependencies ({JsonConvert.SerializeObject(recordedDeps)}) for `{componentId}`!");

            foreach (var expectedDep in expectedDeps)
            {
                recordedDeps.Should().Contain(
                    expectedDep, $"Expected `{expectedDep}` in the list of dependencies for `{componentId}` but only recorded: {JsonConvert.SerializeObject(recordedDeps)}");
            }
        }
    }

    private void CheckChild(IComponentRecorder recorder, string childId, string[] parentIds)
    {
        recorder.AssertAllExplicitlyReferencedComponents<PipComponent>(
            childId,
            parentIds.Select(parentId => new Func<PipComponent, bool>(x => x.Id == parentId)).ToArray());
    }

    private List<(string PackageString, GitComponent Component)> ToGitTuple(IList<string> components)
    {
        return components.Select<string, (string, GitComponent)>(dep => (dep, null)).ToList();
    }
}
