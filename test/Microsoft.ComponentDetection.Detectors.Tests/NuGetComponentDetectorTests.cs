#nullable disable
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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetComponentDetectorTests : BaseDetectorTest<NuGetComponentDetector>
{
    private static readonly IEnumerable<string> DetectorSearchPattern =
        ["*.nupkg", "*.nuspec", "nuget.config", "paket.lock"];

    private readonly Mock<ILogger<NuGetComponentDetector>> mockLogger;

    public NuGetComponentDetectorTests()
    {
        this.mockLogger = new Mock<ILogger<NuGetComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);
    }

    [TestMethod]
    public async Task TestNuGetDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNuspecComponentAsync()
    {
        var testComponentName = "TestComponentName";
        var testVersion = "1.2.3";
        var testAuthors = new string[] { "author 1", "author 2" };
        var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("*.nuspec", nuspec)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success, "Result code does Not match.");
        componentRecorder.GetDetectedComponents().Should().ContainSingle("Component count does not match");
        var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
        detectedComponent.Type.Should().Be(ComponentType.NuGet);
        var nuGetComponent = (NuGetComponent)detectedComponent;
        nuGetComponent.Name.Should().Be(testComponentName, "Component name does not match.");
        nuGetComponent.Version.Should().Be(testVersion, "Component version does not match.");
        nuGetComponent.Authors.Should().BeEquivalentTo(testAuthors, "Authors does not match.");
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNuspecComponent_SingleAuthorAsync()
    {
        var testComponentName = "TestComponentName";
        var testVersion = "1.2.3";
        var testAuthors = new string[] { "author 1" };
        var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("*.nuspec", nuspec)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success, "Result code does Not match.");
        componentRecorder.GetDetectedComponents().Should().ContainSingle("Component count does not match");
        var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
        detectedComponent.Type.Should().Be(ComponentType.NuGet);
        var nuGetComponent = (NuGetComponent)detectedComponent;
        nuGetComponent.Name.Should().Be(testComponentName, "Component name does not match.");
        nuGetComponent.Version.Should().Be(testVersion, "Component version does not match.");
        nuGetComponent.Authors.Should().BeEquivalentTo(testAuthors, "Authors does not match.");
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidNupkgComponentAsync()
    {
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("test.nupkg", nupkg)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestNugetDetector_ReturnsValidMixedComponentAsync()
    {
        var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("test.nuspec", nuspec)
            .WithFile("test.nupkg", nupkg)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);
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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .AddServiceMock(this.mockLogger)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // While there are 26 lines in the sample, several dependencies are identical, so there are only 11 matches.
        componentRecorder.GetDetectedComponents().Should().HaveCount(11);

        // Verify that we stop executing after parsing the paket.lock file.
        this.mockLogger.Verify(
            x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once());
    }

    [TestMethod]
    public async Task TestNugetDetector_HandlesMalformedComponentsInComponentListAsync()
    {
        var validNupkg = await NugetTestUtilities.ZipNupkgComponentAsync("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());
        var malformedNupkg = await NugetTestUtilities.ZipNupkgComponentAsync("malformed.nupkg", NugetTestUtilities.GetRandomMalformedNuPkgComponent());
        var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("test.nuspec", nuspec)
            .WithFile("test.nupkg", validNupkg)
            .WithFile("malformed.nupkg", malformedNupkg)
            .AddServiceMock(this.mockLogger)
            .ExecuteDetectorAsync();

        this.mockLogger.Verify(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestNugetDetector_AdditionalDirectoriesAsync()
    {
        var component1 = NugetTestUtilities.GetRandomValidNuSpecComponentStream();
        var streamsDetectedInNormalPass = new List<IComponentStream> { component1 };

        var additionalDirectory = this.CreateTemporaryDirectory();
        var nugetConfigComponent = NugetTestUtilities.GetValidNuGetConfig(additionalDirectory);
        var streamsDetectedInAdditionalDirectoryPass = new List<IComponentStream> { nugetConfigComponent };

        var componentRecorder = new ComponentRecorder();
        var mockLogger = new Mock<ILogger>();
        var sourceDirectoryPath = this.CreateTemporaryDirectory();

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
                    Match.Create<IEnumerable<string>>(stuff => DetectorSearchPattern.Intersect(stuff).Count() == DetectorSearchPattern.Count()),
                    It.IsAny<ExcludeDirectoryPredicate>(),
                    It.IsAny<bool>()))
            .Returns([]);

        // This is matching the additional directory that is ONLY sourced in the nuget.config. If this works, we would see the component in our results.
        componentStreamEnumerableFactoryMock.Setup(
                x => x.GetComponentStreams(
                    Match.Create<DirectoryInfo>(info => info.FullName.Contains(additionalDirectory)),
                    Match.Create<IEnumerable<string>>(stuff => DetectorSearchPattern.Intersect(stuff).Count() == DetectorSearchPattern.Count()),
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

        var detector = new NuGetComponentDetector(
            componentStreamEnumerableFactoryMock.Object,
            directoryWalkerMock.Object,
            new Mock<ILogger<NuGetComponentDetector>>().Object);

        var scanResult = await detector.ExecuteDetectorAsync(new ScanRequest(new DirectoryInfo(sourceDirectoryPath), (name, directoryName) => false, null, new Dictionary<string, string>(), null, componentRecorder));

        directoryWalkerMock.VerifyAll();
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestNugetDetector_LowConfidencePackagesAsync()
    {
        var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync("Newtonsoft.Json.nupkg", NugetTestUtilities.GetValidNuspec("Newtonsoft.Json", "9.0.1", ["JamesNK"]));

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Newtonsoft.Json.nupkg", nupkg)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty()
            .And.HaveCount(0);
    }

    private string CreateTemporaryDirectory()
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
