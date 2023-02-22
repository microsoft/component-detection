namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NpmDetectorTests : BaseDetectorTest<NpmComponentDetector>
{
    private readonly List<string> packageJsonSearchPattern = new List<string> { "package.json" };

    [TestMethod]
    public async Task TestNpmDetector_NameAndVersionDetectedAsync()
    {
        var componentName = GetRandomString();
        var version = NewRandomVersion();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForNameAndVersion(componentName, version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(1, detectedComponents.Count());
        Assert.AreEqual(detectedComponents.First().Component.Type, ComponentType.Npm);
        Assert.AreEqual(((NpmComponent)detectedComponents.First().Component).Name, componentName);
        Assert.AreEqual(((NpmComponent)detectedComponents.First().Component).Version, version);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameAndEmailDetected_AuthorInJsonFormatAsync()
    {
        var authorName = GetRandomString();
        var authorEmail = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, authorEmail);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameDetectedWhenEmailIsNotPresent_AuthorInJsonFormatAsync()
    {
        var authorName = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, null);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameAndAuthorEmailDetected_WhenAuthorNameAndEmailAndUrlIsPresent_AuthorAsSingleStringAsync()
    {
        var authorName = GetRandomString();
        var authorEmail = GetRandomString();
        var authroUrl = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail, authroUrl);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameDetected_WhenEmailNotPresentAndUrlIsPresent_AuthorAsSingleStringAsync()
    {
        var authorName = GetRandomString();
        var authroUrl = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, null, authroUrl);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNull_WhenAuthorMalformed_AuthorAsSingleStringAsync()
    {
        var authorName = GetRandomString();
        var authroUrl = GetRandomString();
        var authorEmail = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesMalformedAuthorAsSingleString(authorName, authorEmail, authroUrl);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameDetected_WhenEmailNotPresentAndUrlNotPresent_AuthorAsSingleStringAsync()
    {
        var authorName = GetRandomString();
        var authroUrl = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();
        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_AuthorNameAndAuthorEmailDetected_WhenUrlNotPresent_AuthorAsSingleStringAsync()
    {
        var authorName = GetRandomString();
        var authorEmail = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.AreEqual(authorName, ((NpmComponent)detectedComponents.First().Component).Author.Name);
        Assert.AreEqual(authorEmail, ((NpmComponent)detectedComponents.First().Component).Author.Email);
    }

    [TestMethod]
    public async Task TestNpmDetector_NullAuthor_WhenAuthorNameIsNullOrEmpty_AuthorAsJsonAsync()
    {
        var authorName = string.Empty;
        var authorEmail = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(authorName, authorEmail);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        Assert.IsNull(((NpmComponent)detectedComponents.First().Component).Author);
    }

    [TestMethod]
    public async Task TestNpmDetector_NullAuthor_WhenAuthorNameIsNullOrEmpty_AuthorAsSingleStringAsync()
    {
        var authorName = string.Empty;
        var authorEmail = GetRandomString();
        var (packageJsonName, packageJsonContents, packageJsonPath) =
            NpmTestUtilities.GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(authorName, authorEmail);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

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
