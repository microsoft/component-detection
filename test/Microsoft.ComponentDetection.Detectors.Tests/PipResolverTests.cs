#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PipResolverTests
{
    private Mock<ILogger<PythonResolver>> loggerMock;
    private Mock<IPyPiClient> pyPiClient;
    private Mock<ISingleFileComponentRecorder> recorderMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.loggerMock = new Mock<ILogger<PythonResolver>>();
        this.pyPiClient = new Mock<IPyPiClient>();
        this.recorderMock = new Mock<ISingleFileComponentRecorder>();
    }

    [TestMethod]
    public async Task TestPipResolverSimpleGraphAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c==1.0");

        var versions = new List<string> { "1.0" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(versions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
            Info = new PythonProjectInfo
            {
                Author = "Microsoft",
                License = "MIT",
            },
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
            Info = new PythonProjectInfo
            {
                AuthorEmail = "Microsoft <sample@microsoft.com>",
                Classifiers = ["License :: OSI Approved :: MIT License"],
            },
        };

        var cProject = new PythonProject
        {
            Releases = cReleases,
            Info = new PythonProjectInfo
            {
                Maintainer = "Microsoft",
                Classifiers = ["License :: OSI Approved :: MIT License", "License :: OSI Approved :: BSD License"],
            },
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(cProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([c]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync([]);

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0", "Microsoft", "MIT"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0", "Microsoft <sample@microsoft.com>", "MIT License"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0", "Microsoft", "MIT License, BSD License"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPipResolverNonExistantRootAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c==1.0");
        var doesNotExist = new PipDependencySpecification("dne==1.0");

        var versions = new List<string> { "1.0" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(versions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
            Info = new PythonProjectInfo
            {
                MaintainerEmail = "Microsoft",
            },
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
        };

        var cProject = new PythonProject
        {
            Releases = cReleases,
        };

        var dneProject = new PythonProject
        {
            Releases = [],
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(cProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(doesNotExist)).ReturnsAsync(dneProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([c]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync([]);

        var dependencies = new List<PipDependencySpecification> { a, doesNotExist };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0", "Microsoft", null));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPipResolverInvalidSpecAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c==1.0");
        var doesNotExist = new PipDependencySpecification("dne==1.0");

        var versions = new List<string> { "1.0" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(versions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
            Info = new PythonProjectInfo
            {
                MaintainerEmail = "Microsoft",
            },
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
        };

        var cProject = new PythonProject
        {
            Releases = cReleases,
        };

        var dneProject = new PythonProject
        {
            Releases = [],
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(cProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(doesNotExist)).ReturnsAsync(dneProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([c]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync([]);

        var dependencies = new List<PipDependencySpecification> { a, doesNotExist };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0", "Microsoft", null));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();

        this.loggerMock.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => string.Equals("Unable to resolve root dependency dne with version specifiers [\"==1.0\"] from pypi possibly due to computed version constraints. Skipping package.", o.ToString(), StringComparison.Ordinal)),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipResolverNonExistantLeafAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c==1.0");

        var versions = new List<string> { "1.0" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(versions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
        };

        var dneProject = new PythonProject
        {
            Releases = [],
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(dneProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([c]);

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();
        this.pyPiClient.Verify(x => x.FetchPackageDependenciesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PythonProjectRelease>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task TestPipResolverBacktrackAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c<=1.1");
        var cAlt = new PipDependencySpecification("c==1.0");

        var versions = new List<string> { "1.0" };

        var otherVersions = new List<string> { "1.0", "1.1" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(otherVersions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
        };

        var cProject = new PythonProject
        {
            Releases = cReleases,
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(cProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b, c]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([cAlt]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.1", cReleases["1.1"].First())).ReturnsAsync([]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync([]);

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedA.Children.Add(expectedC);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedA);
        expectedC.Parents.Add(expectedB);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();
        this.pyPiClient.Verify(x => x.FetchPackageDependenciesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PythonProjectRelease>()), Times.Exactly(4));
    }

    [TestMethod]
    public async Task TestInvalidVersionSpecThrowsAsync()
    {
        var a = new PipDependencySpecification("a==1.0");
        var b = new PipDependencySpecification("b==1.0");
        var c = new PipDependencySpecification("c==1.0");
        var c2 = new PipDependencySpecification("Requires-Dist: c (>dev)", true);

        var versions = new List<string> { "1.0" };

        var aReleases = this.CreateReleasesDictionary(versions);
        var bReleases = this.CreateReleasesDictionary(versions);
        var cReleases = this.CreateReleasesDictionary(versions);

        var aProject = new PythonProject
        {
            Releases = aReleases,
            Info = new PythonProjectInfo
            {
                Author = "Microsoft",
                License = "MIT",
            },
        };

        var bProject = new PythonProject
        {
            Releases = bReleases,
            Info = new PythonProjectInfo
            {
                AuthorEmail = "Microsoft <sample@microsoft.com>",
                Classifiers = ["License :: OSI Approved :: MIT License"],
            },
        };

        var cProject = new PythonProject
        {
            Releases = cReleases,
            Info = new PythonProjectInfo
            {
                Maintainer = "Microsoft",
                Classifiers = ["License :: OSI Approved :: MIT License", "License :: OSI Approved :: BSD License"],
            },
        };

        this.pyPiClient.Setup(x => x.GetProjectAsync(a)).ReturnsAsync(aProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(b)).ReturnsAsync(bProject);
        this.pyPiClient.Setup(x => x.GetProjectAsync(c)).ReturnsAsync(cProject);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync([b]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync([c, c2]);
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync([]);

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        resolveResult.Should().NotBeNull();

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0", "Microsoft", "MIT"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0", "Microsoft <sample@microsoft.com>", "MIT License"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0", "Microsoft", "MIT License, BSD License"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        this.CompareGraphs(resolveResult.First(), expectedA).Should().BeTrue();

        this.loggerMock.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => string.Equals("Failure resolving Python package c with message: The version specification dev is not a valid python version.", o.ToString(), StringComparison.Ordinal)),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    private bool CompareGraphs(PipGraphNode a, PipGraphNode b)
    {
        var componentA = a.Value;
        var componentB = b.Value;

        if (!string.Equals(componentA.Name, componentB.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(componentA.Version, componentB.Version, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(componentA.License, componentB.License, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(componentA.Author, componentB.Author, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (a.Children.Count != b.Children.Count)
        {
            return false;
        }

        var valid = true;

        for (var i = 0; i < a.Children.Count; i++)
        {
            valid = this.CompareGraphs(a.Children[i], b.Children[i]);
        }

        return valid;
    }

    private SortedDictionary<string, IList<PythonProjectRelease>> CreateReleasesDictionary(IList<string> versions)
    {
        var toReturn = new SortedDictionary<string, IList<PythonProjectRelease>>(new PythonVersionComparer());

        foreach (var version in versions)
        {
            toReturn.Add(
                version,
                [
                    this.CreatePythonProjectRelease(),
                ]);
        }

        return toReturn;
    }

    private PythonProjectRelease CreatePythonProjectRelease()
    {
        return new PythonProjectRelease { PackageType = "bdist_wheel", PythonVersion = "3.5.2", Size = 1000, Url = new Uri($"https://{Guid.NewGuid()}") };
    }
}
