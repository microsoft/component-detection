namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetComponentDetectorTests
{
    private Mock<ILogger> loggerMock;
    private DetectorTestUtility<NuGetComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        this.loggerMock = new Mock<ILogger>();
        this.detectorTestUtility = DetectorTestUtilityCreator.Create<NuGetComponentDetector>();
    }

    [TestMethod]
    public async Task TestNuGetDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNuspecComponentAsync()
    {
        var testComponentName = "TestComponentName";
        var testVersion = "1.2.3";
        var testAuthors = new string[] { "author 1", "author 2" };
        var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("*.nuspec", nuspec)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode, "Result code does Not match.");
        Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count(), "Componet count does not match");
        var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
        Assert.AreEqual(ComponentType.NuGet, detectedComponent.Type);
        var nuGetComponent = (NuGetComponent)detectedComponent;
        Assert.AreEqual(testComponentName, nuGetComponent.Name, "Component name does not match.");
        Assert.AreEqual(testVersion, nuGetComponent.Version, "Component version does not match.");
        CollectionAssert.AreEqual(testAuthors, nuGetComponent.Authors, "Authors does not match.");
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNuspecComponent_SingleAuthorAsync()
    {
        var testComponentName = "TestComponentName";
        var testVersion = "1.2.3";
        var testAuthors = new string[] { "author 1" };
        var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("*.nuspec", nuspec)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode, "Result code does Not match.");
        Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count(), "Componet count does not match");
        var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
        Assert.AreEqual(ComponentType.NuGet, detectedComponent.Type);
        var nuGetComponent = (NuGetComponent)detectedComponent;
        Assert.AreEqual(testComponentName, nuGetComponent.Name, "Component name does not match.");
        Assert.AreEqual(testVersion, nuGetComponent.Version, "Component version does not match.");
        CollectionAssert.AreEqual(testAuthors, nuGetComponent.Authors, "Authors does not match.");
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNupkgComponentAsync()
    {
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("test.nupkg", nupkg)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidMixedComponentAsync()
    {
        var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("test.nuspec", nuspec)
            .WithFile("test.nupkg", nupkg)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidPaketComponentAsync()
    {
        var paketLock = @"
NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (3.3.0)
    Castle.Core-log4net (3.3.0)
      Castle.Core (>= 3.3.0)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    Rx-Core (2.2.5)
      Rx-Interfaces (>= 2.2.5)
    Rx-Interfaces (2.2.5)
    Rx-Linq (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
    Rx-Main (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
      Rx-Linq (>= 2.2.5)
      Rx-PlatformServices (>= 2.2.5)
    Rx-PlatformServices (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
    log4net (1.2.10)
            ";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        // While there are 26 lines in the sample, several dependencies are identical, so there are only 11 matches.
        Assert.AreEqual(11, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_HandlesMalformedComponentsInComponentListAsync()
    {
        var validNupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());
        var malformedNupkg = await NugetTestUtilities.ZipNupkgComponentAsync("malformed.nupkg", NugetTestUtilities.GetRandomMalformedNuPkgComponent());
        var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithLogger(this.loggerMock)
            .WithFile("test.nuspec", nuspec)
            .WithFile("test.nupkg", validNupkg)
            .WithFile("malformed.nupkg", malformedNupkg)
            .ExecuteDetectorAsync();

        this.loggerMock.Verify(x => x.LogFailedReadingFile(Path.Join(Path.GetTempPath(), "malformed.nupkg"), It.IsAny<Exception>()));

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_AdditionalDirectoriesAsync()
    {
        var component1 = NugetTestUtilities.GetRandomValidNuSpecComponentStream();
        var streamsDetectedInNormalPass = new List<IComponentStream> { component1 };

        var additionalDirectory = CreateTemporaryDirectory();
        var nugetConfigComponent = NugetTestUtilities.GetValidNuGetConfig(additionalDirectory);
        var streamsDetectedInAdditionalDirectoryPass = new List<IComponentStream> { nugetConfigComponent };

        var componentRecorder = new ComponentRecorder();
        var detector = new NuGetComponentDetector();
        var sourceDirectoryPath = CreateTemporaryDirectory();

        detector.Logger = this.loggerMock.Object;

        // Use strict mock evaluation because we're doing some "fun" stuff with this mock.
        var componentStreamEnumerableFactoryMock = new Mock<IComponentStreamEnumerableFactory>(MockBehavior.Strict);
        var directoryWalkerMock = new Mock<IObservableDirectoryWalkerFactory>(MockBehavior.Strict);

        directoryWalkerMock.Setup(x => x.Initialize(It.IsAny<DirectoryInfo>(), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<int>(), It.IsAny<IEnumerable<string>>()));

        // First setup is for the invocation of stream enumerable factory used to find NuGet.Configs -- a special case the detector supports to locate repos located outside the source dir
        //  We return a nuget config that targets a different temp folder that is NOT in a subtree of the sourcedirectory.
        componentStreamEnumerableFactoryMock.Setup(
                x => x.GetComponentStreams(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(sourceDirectoryPath)),
                    Match.Create<IEnumerable<string>>(stuff => stuff.Contains(NuGetComponentDetector.NugetConfigFileName)),
                    It.IsAny<ExcludeDirectoryPredicate>(),
                    It.IsAny<bool>()))
            .Returns(streamsDetectedInAdditionalDirectoryPass);

        // Normal detection setup here -- we have it returning empty.
        componentStreamEnumerableFactoryMock.Setup(
                x => x.GetComponentStreams(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(sourceDirectoryPath)),
                    Match.Create<IEnumerable<string>>(stuff => detector.SearchPatterns.Intersect(stuff).Count() == detector.SearchPatterns.Count),
                    It.IsAny<ExcludeDirectoryPredicate>(),
                    It.IsAny<bool>()))
            .Returns(Enumerable.Empty<IComponentStream>());

        // This is matching the additional directory that is ONLY sourced in the nuget.config. If this works, we would see the component in our results.
        componentStreamEnumerableFactoryMock.Setup(
                x => x.GetComponentStreams(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(additionalDirectory)),
                    Match.Create<IEnumerable<string>>(stuff => detector.SearchPatterns.Intersect(stuff).Count() == detector.SearchPatterns.Count),
                    It.IsAny<ExcludeDirectoryPredicate>(),
                    It.IsAny<bool>()))
            .Returns(streamsDetectedInNormalPass);

        // Normal detection setup here -- we have it returning empty.
        directoryWalkerMock.Setup(
                x => x.GetFilteredComponentStreamObservable(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(sourceDirectoryPath)),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IComponentRecorder>()))
            .Returns(() => streamsDetectedInAdditionalDirectoryPass.Select(cs => new ProcessRequest { ComponentStream = cs, SingleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(cs.Location) }).ToObservable());

        // This is matching the additional directory that is ONLY sourced in the nuget.config. If this works, we would see the component in our results.
        directoryWalkerMock.Setup(
                x => x.GetFilteredComponentStreamObservable(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(additionalDirectory)),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<ComponentRecorder>()))
            .Returns(() => streamsDetectedInNormalPass.Select(cs => new ProcessRequest { ComponentStream = cs, SingleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(cs.Location) }).ToObservable());

        detector.ComponentStreamEnumerableFactory = componentStreamEnumerableFactoryMock.Object;
        detector.Scanner = directoryWalkerMock.Object;

        var scanResult = await detector.ExecuteDetectorAsync(new ScanRequest(new DirectoryInfo(sourceDirectoryPath), (name, directoryName) => false, null, new Dictionary<string, string>(), null, componentRecorder));

        directoryWalkerMock.VerifyAll();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNugetDetector_LowConfidencePackagesAsync()
    {
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("Newtonsoft.Json.nupkg", NugetTestUtilities.GetValidNuspec("Newtonsoft.Json", "9.0.1", new[] { "JamesNK" }));

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("Newtonsoft.Json.nupkg", nupkg)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty()
            .And.HaveCount(0);
    }

    private static string CreateTemporaryDirectory()
    {
        string path;
        do
        {
            path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }
        while (Directory.Exists(path) || File.Exists(path));

        Directory.CreateDirectory(path);
        return path;
    }
}
