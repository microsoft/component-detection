namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        this.pyPiClient.Setup(x => x.GetReleasesAsync(a)).ReturnsAsync(aReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(b)).ReturnsAsync(bReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(c)).ReturnsAsync(cReleases);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { b });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { c });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { });

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
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

        this.pyPiClient.Setup(x => x.GetReleasesAsync(a)).ReturnsAsync(aReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(b)).ReturnsAsync(bReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(c)).ReturnsAsync(cReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(doesNotExist)).ReturnsAsync(this.CreateReleasesDictionary(new List<string>()));

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { b });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { c });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { });

        var dependencies = new List<PipDependencySpecification> { a, doesNotExist };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
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

        this.pyPiClient.Setup(x => x.GetReleasesAsync(a)).ReturnsAsync(aReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(b)).ReturnsAsync(bReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(c)).ReturnsAsync(this.CreateReleasesDictionary(new List<string>()));

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { b });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { c });

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
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

        this.pyPiClient.Setup(x => x.GetReleasesAsync(a)).ReturnsAsync(aReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(b)).ReturnsAsync(bReleases);
        this.pyPiClient.Setup(x => x.GetReleasesAsync(c)).ReturnsAsync(cReleases);

        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("a", "1.0", aReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { b, c });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("b", "1.0", bReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { cAlt });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.1", cReleases["1.1"].First())).ReturnsAsync(new List<PipDependencySpecification> { });
        this.pyPiClient.Setup(x => x.FetchPackageDependenciesAsync("c", "1.0", cReleases["1.0"].First())).ReturnsAsync(new List<PipDependencySpecification> { });

        var dependencies = new List<PipDependencySpecification> { a };

        var resolver = new PythonResolver(this.pyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));
        var expectedC = new PipGraphNode(new PipComponent("c", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedA.Children.Add(expectedC);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedA);
        expectedC.Parents.Add(expectedB);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
        this.pyPiClient.Verify(x => x.FetchPackageDependenciesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PythonProjectRelease>()), Times.Exactly(4));
    }

    private bool CompareGraphs(PipGraphNode a, PipGraphNode b)
    {
        var componentA = a.Value;
        var componentB = b.Value;

        if (!string.Equals(componentA.Name, componentB.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(componentA.Version, componentB.Version, StringComparison.OrdinalIgnoreCase))
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
            toReturn.Add(version, new List<PythonProjectRelease>
            {
                this.CreatePythonProjectRelease(),
            });
        }

        return toReturn;
    }

    private PythonProjectRelease CreatePythonProjectRelease()
    {
        return new PythonProjectRelease { PackageType = "bdist_wheel", PythonVersion = "3.5.2", Size = 1000, Url = new Uri($"https://{Guid.NewGuid()}") };
    }
}
