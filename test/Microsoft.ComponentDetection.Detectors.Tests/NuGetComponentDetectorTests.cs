using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
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
            loggerMock = new Mock<ILogger>();
            detectorTestUtility = DetectorTestUtilityCreator.Create<NuGetComponentDetector>();
        }

        [TestMethod]
        public async Task TestNuGetDetectorWithNoFiles_ReturnsSuccessfully()
        {
            var (scanResult, componentRecorder) = await detectorTestUtility.ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNugetDetector_ReturnsValidNuspecComponent()
        {
            var testComponentName = "TestComponentName";
            var testVersion = "1.2.3";
            var testAuthors = new string[] { "author 1", "author 2" };
            var testTargetFrameworks = new string[] { "net48", "net472", "netstandard2.1" };
            var testTargetFrameworksConverted = new string[] { ".NETFramework,Version=v4.8", ".NETFramework,Version=v4.7.2", ".NETStandard,Version=v2.1" };
            var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors, testTargetFrameworks);

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("*.nuspec", nuspec)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode, "Result code does Not match.");
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count(), "Componet count does not match");
            var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
            Assert.AreEqual(Contracts.TypedComponent.ComponentType.NuGet, detectedComponent.Type);
            var nuGetComponent = (NuGetComponent)detectedComponent;
            Assert.AreEqual(testComponentName, nuGetComponent.Name, "Component name does not match.");
            Assert.AreEqual(testVersion, nuGetComponent.Version, "Component version does not match.");
            CollectionAssert.AreEqual(testAuthors, nuGetComponent.Authors, "Authors does not match.");
            CollectionAssert.AreEqual(testTargetFrameworksConverted, nuGetComponent.TargetFrameworks, "TargetFrameworks does not match.");
        }

        [TestMethod]
        public async Task TestNugetDetector_ReturnsValidNuspecComponent_SingleAuthor()
        {
            var testComponentName = "TestComponentName";
            var testVersion = "1.2.3";
            var testAuthors = new string[] { "author 1" };
            var nuspec = NugetTestUtilities.GetValidNuspec(testComponentName, testVersion, testAuthors);

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("*.nuspec", nuspec)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode, "Result code does Not match.");
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count(), "Componet count does not match");
            var detectedComponent = componentRecorder.GetDetectedComponents().First().Component;
            Assert.AreEqual(Contracts.TypedComponent.ComponentType.NuGet, detectedComponent.Type);
            var nuGetComponent = (NuGetComponent)detectedComponent;
            Assert.AreEqual(testComponentName, nuGetComponent.Name, "Component name does not match.");
            Assert.AreEqual(testVersion, nuGetComponent.Version, "Component version does not match.");
            CollectionAssert.AreEqual(testAuthors, nuGetComponent.Authors, "Authors does not match.");
        }

        [TestMethod]
        public async Task TestNugetDetector_ReturnsValidNupkgComponent()
        {
            var nupkg = await NugetTestUtilities.ZipNupkgComponent("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("test.nupkg", nupkg)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNugetDetector_ReturnsValidMixedComponent()
        {
            var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();
            var nupkg = await NugetTestUtilities.ZipNupkgComponent("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("test.nuspec", nuspec)
                                                    .WithFile("test.nupkg", nupkg)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNugetDetector_HandlesMalformedComponentsInComponentList()
        {
            var validNupkg = await NugetTestUtilities.ZipNupkgComponent("test.nupkg", NugetTestUtilities.GetRandomValidNuspec());
            var malformedNupkg = await NugetTestUtilities.ZipNupkgComponent("malformed.nupkg", NugetTestUtilities.GetRandomMalformedNuPkgComponent());
            var nuspec = NugetTestUtilities.GetRandomValidNuSpecComponent();

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithLogger(loggerMock)
                                                    .WithFile("test.nuspec", nuspec)
                                                    .WithFile("test.nupkg", validNupkg)
                                                    .WithFile("malformed.nupkg", malformedNupkg)
                                                    .ExecuteDetector();

            loggerMock.Verify(x => x.LogFailedReadingFile(Path.Join(Path.GetTempPath(), "malformed.nupkg"), It.IsAny<Exception>()));

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNugetDetector_AdditionalDirectories()
        {
            var component1 = NugetTestUtilities.GetRandomValidNuSpecComponentStream();
            var streamsDetectedInNormalPass = new List<IComponentStream> { component1 };

            var additionalDirectory = CreateTemporaryDirectory();
            var nugetConfigComponent = NugetTestUtilities.GetValidNuGetConfig(additionalDirectory);
            var streamsDetectedInAdditionalDirectoryPass = new List<IComponentStream> { nugetConfigComponent };

            var componentRecorder = new ComponentRecorder();
            var detector = new NuGetComponentDetector();
            var sourceDirectoryPath = CreateTemporaryDirectory();

            detector.Logger = loggerMock.Object;

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
}
