using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class NpmDetectorTests
    {
        private Mock<ILogger> loggerMock;
        private Mock<IPathUtilityService> pathUtilityService;
        private ComponentRecorder componentRecorder;
        private DetectorTestUtility<NpmComponentDetector> detectorTestUtility = DetectorTestUtilityCreator.Create<NpmComponentDetector>();
        private List<string> packageJsonSearchPattern = new List<string> { "package.json" };

        [TestInitialize]
        public void TestInitialize()
        {
            this.loggerMock = new Mock<ILogger>();
            this.pathUtilityService = new Mock<IPathUtilityService>();
            this.pathUtilityService.Setup(x => x.GetParentDirectory(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
            this.componentRecorder = new ComponentRecorder();
        }

        [TestMethod]
        public async Task TestNpmDetector_NameAndVersionDetected()
        {
            var componentName = GetRandomString();
            var version = NewRandomVersion();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForNameAndVersion(componentName, version);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(detectedComponents.First().Component.Type, ComponentType.Npm);
            Assert.AreEqual(((NpmComponent)detectedComponents.First().Component).Name, componentName);
            Assert.AreEqual(((NpmComponent)detectedComponents.First().Component).Version, version);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameAndEmailDetected_AuthorInJsonFormat()
        {
            var authorName = GetRandomString();
            var authorEmail = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, authorEmail);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameDetectedWhenEmailIsNotPresent_AuthorInJsonFormat()
        {
            var authorName = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, null);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameAndAuthorEmailDetected_WhenAuthorNameAndEmailAndUrlIsPresent_AuthorAsSingleString()
        {
            var authorName = GetRandomString();
            var authorEmail = GetRandomString();
            var authroUrl = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail, authroUrl);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameDetected_WhenEmailNotPresentAndUrlIsPresent_AuthorAsSingleString()
        {
            var authorName = GetRandomString();
            var authroUrl = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, null, authroUrl);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNull_WhenAuthorMalformed_AuthorAsSingleString()
        {
            var authorName = GetRandomString();
            var authroUrl = GetRandomString();
            var authorEmail = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesMalformedAuthorAsSingleString(authorName, authorEmail, authroUrl);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameDetected_WhenEmailNotPresentAndUrlNotPresent_AuthorAsSingleString()
        {
            var authorName = GetRandomString();
            var authroUrl = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();
            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_AuthorNameAndAuthorEmailDetected_WhenUrlNotPresent_AuthorAsSingleString()
        {
            var authorName = GetRandomString();
            var authorEmail = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
            Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
        }

        [TestMethod]
        public async Task TestNpmDetector_NullAuthor_WhenAuthorNameIsNullOrEmpty_AuthorAsJson()
        {
            var authorName = string.Empty;
            var authorEmail = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, authorEmail);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author);
        }

        [TestMethod]
        public async Task TestNpmDetector_NullAuthor_WhenAuthorNameIsNullOrEmpty_AuthorAsSingleString()
        {
            var authorName = string.Empty;
            var authorEmail = GetRandomString();
            var (packageJsonName, packageJsonContents, packageJsonPath) =
                NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail);
            var detector = new NpmComponentDetector();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithDetector(detector)
                .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
                .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            AssertDetectedComponentCount(detectedComponents, 1);
            AssertNpmComponent(detectedComponents);
            Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author);
        }

        private static void AssertDetectedComponentCount(IEnumerable<DetectedComponent> detectedComponents, int expectedCount)
        {
            Assert.AreEqual(expectedCount, detectedComponents.Count());
        }

        private static void AssertNpmComponent(IEnumerable<DetectedComponent> detectedComponents)
        {
            Assert.AreEqual(detectedComponents.First().Component.Type, ComponentType.Npm);
        }
    }
}
