namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class SimplePythonResolverTests
{
    private Mock<ILogger<SimplePythonResolver>> loggerMock;
    private Mock<ISimplePyPiClient> simplePyPiClient;
    private Mock<ISingleFileComponentRecorder> recorderMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.loggerMock = new Mock<ILogger<SimplePythonResolver>>();
        this.simplePyPiClient = new Mock<ISimplePyPiClient>();
        this.recorderMock = new Mock<ISingleFileComponentRecorder>();
    }

    [TestMethod]
    public async Task TestPipResolverSimpleGraphAsync()
    {
        var a = "a==1.0";
        var b = "b==1.0";
        var c = "c==1.0";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);

        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c")))).ReturnsAsync(cReleases);

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.0", this.CreateMetadataString(new List<string>() { b })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.0", this.CreateMetadataString(new List<string>() { c })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.First().Url)).ReturnsAsync(new MemoryStream());

        var dependencies = new List<PipDependencySpecification> { specA };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

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
        var a = "a==1.0";
        var b = "b==1.0";
        var c = "c==1.0";
        var doesNotExist = "dne==1.0";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);
        var specDne = new PipDependencySpecification(doesNotExist);

        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c")))).ReturnsAsync(cReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("dne")))).ReturnsAsync(this.CreateSimplePypiProject(new List<(string, string)>()));

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.0", this.CreateMetadataString(new List<string>() { b })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.0", this.CreateMetadataString(new List<string>() { c })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.First().Url)).ReturnsAsync(new MemoryStream());

        var dependencies = new List<PipDependencySpecification> { specA, specDne };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

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
        var a = "a==1.0";
        var b = "b==1.0";
        var c = "c==1.0";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);
        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c")))).ReturnsAsync(cReleases);

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.0", this.CreateMetadataString(new List<string>() { b })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.0", this.CreateMetadataString(new List<string>() { c })));

        var dependencies = new List<PipDependencySpecification> { specA };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
        this.simplePyPiClient.Verify(x => x.FetchPackageFileStreamAsync(It.IsAny<Uri>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task TestPipResolverBacktrackAsync()
    {
        var a = "a==1.0";
        var b = "b==1.0";
        var c = "c<=1.1";
        var cAlt = "c==1.0";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);
        var specCAlt = new PipDependencySpecification(cAlt);

        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel"), ("1.1", "bdist_wheel") });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c") && x.DependencySpecifiers.First().Equals("<=1.1")))).ReturnsAsync(cReleases);

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.0", this.CreateMetadataString(new List<string>() { b, c })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.0", this.CreateMetadataString(new List<string>() { cAlt })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.First().Url)).ReturnsAsync(new MemoryStream());
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.Last().Url)).ReturnsAsync(new MemoryStream());

        var dependencies = new List<PipDependencySpecification> { specA };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

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
        this.simplePyPiClient.Verify(x => x.FetchPackageFileStreamAsync(It.IsAny<Uri>()), Times.Exactly(4));
    }

    [TestMethod]
    public async Task TestPipResolverVersionExtractionWithDifferentVersionFormatsAsync()
    {
        var a = "a==1.15.0";
        var b = "b==1.19";
        var c = "c==3.1.1";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);

        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.15.0", "bdist_wheel") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.19", "bdist_wheel") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("3.1.1", "bdist_wheel") });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c")))).ReturnsAsync(cReleases);

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.15.0", this.CreateMetadataString(new List<string>() { b })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.19", this.CreateMetadataString(new List<string>() { c })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.First().Url)).ReturnsAsync(new MemoryStream());

        var dependencies = new List<PipDependencySpecification> { specA };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.15.0"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.19"));
        var expectedC = new PipGraphNode(new PipComponent("c", "3.1.1"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);
        expectedB.Children.Add(expectedC);
        expectedC.Parents.Add(expectedB);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
    }

    [TestMethod]
    public async Task TestPipResolverVersionExtractionWithDifferentPackageTypesAsync()
    {
        var a = "a==1.20";
        var b = "b==1.0.0";
        var c = "c==1.0";

        var specA = new PipDependencySpecification(a);
        var specB = new PipDependencySpecification(b);
        var specC = new PipDependencySpecification(c);

        var aReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.20", "bdist_egg") });
        var bReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0.0", "sdist") });
        var cReleases = this.CreateSimplePypiProject(new List<(string, string)> { ("1.0", "bdist_wheel") });

        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("a")))).ReturnsAsync(aReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("b")))).ReturnsAsync(bReleases);
        this.simplePyPiClient.Setup(x => x.GetSimplePypiProjectAsync(It.Is<PipDependencySpecification>(x => x.Name.Equals("c")))).ReturnsAsync(cReleases);

        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(aReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("a", "1.20", this.CreateMetadataString(new List<string>() { b })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(bReleases.Files.First().Url)).ReturnsAsync(this.CreatePypiZip("b", "1.0.0", this.CreateMetadataString(new List<string>() { c })));
        this.simplePyPiClient.Setup(x => x.FetchPackageFileStreamAsync(cReleases.Files.First().Url)).ReturnsAsync(new MemoryStream());

        var dependencies = new List<PipDependencySpecification> { specA };

        var resolver = new SimplePythonResolver(this.simplePyPiClient.Object, this.loggerMock.Object);

        var resolveResult = await resolver.ResolveRootsAsync(this.recorderMock.Object, dependencies);

        Assert.IsNotNull(resolveResult);

        var expectedA = new PipGraphNode(new PipComponent("a", "1.20"));
        var expectedB = new PipGraphNode(new PipComponent("b", "1.0.0"));

        expectedA.Children.Add(expectedB);
        expectedB.Parents.Add(expectedA);

        Assert.IsTrue(this.CompareGraphs(resolveResult.First(), expectedA));
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

    private SimplePypiProject CreateSimplePypiProject(IList<(string Version, string PackageTypes)> versionAndTypes)
    {
        var toReturn = new SimplePypiProject() { Files = new List<SimplePypiProjectRelease>() };

        foreach ((var version, var packagetype) in versionAndTypes)
        {
            toReturn.Files.Add(this.CreateSimplePythonProjectRelease(version, packagetype));
        }

        return toReturn;
    }

    private SimplePypiProjectRelease CreateSimplePythonProjectRelease(string version, string packageType = "bdist_wheel")
    {
        var releaseDict = new Dictionary<string, SimplePypiProjectRelease>();
        var fileExt = string.Empty;
        if (packageType == "bdist_wheel")
        {
            fileExt = "-py2.py3-none-any.whl";
        }
        else if (packageType == "sdist")
        {
            fileExt = ".tar.gz";
        }
        else
        {
            fileExt = "-py3.5.egg";
        }

        return new SimplePypiProjectRelease { FileName = string.Format("google-cloud-secret-manager-{0}{1}", version, fileExt), Size = 1000, Url = new Uri($"https://{Guid.NewGuid()}") };
    }

    private string CreateMetadataString(IList<string> dependency)
    {
        var metadataFile = @"Metadata-Version: 2.0
Name: boto3
Version: 1.10.9
Summary: The AWS SDK for Python
Home-page: https://github.com/boto/boto3
Author: Amazon Web Services
Author-email: UNKNOWN
License: Apache License 2.0
Platform: UNKNOWN
Classifier: Development Status :: 5 - Production/Stable
Classifier: Intended Audience :: Developers
Classifier: Natural Language :: English
Classifier: License :: OSI Approved :: Apache Software License
Classifier: Programming Language :: Python
Classifier: Programming Language :: Python :: 2.6
Classifier: Programming Language :: Python :: 2.7
Classifier: Programming Language :: Python :: 3
Classifier: Programming Language :: Python :: 3.3
Classifier: Programming Language :: Python :: 3.4
Classifier: Programming Language :: Python :: 3.5
Classifier: Programming Language :: Python :: 3.6
Classifier: Programming Language :: Python :: 3.7";

        foreach (var dep in dependency)
        {
            metadataFile += Environment.NewLine + string.Format("Requires-Dist: {0}", dep);
        }

        return metadataFile;
    }

    private Stream CreatePypiZip(string name, string version, string content)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry($"{name.Replace('-', '_')}-{version}.dist-info/METADATA");

            using var entryStream = entry.Open();

            var templateBytes = Encoding.UTF8.GetBytes(content);
            entryStream.Write(templateBytes);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}
