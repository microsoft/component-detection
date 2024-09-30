namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
    private readonly List<string> packageJsonSearchPattern = ["package.json"];

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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();
        ComponentType.Npm.Should().Be(detectedComponents.First().Component.Type);
        componentName.Should().Be(((NpmComponent)detectedComponents.First().Component).Name);
        version.Should().Be(((NpmComponent)detectedComponents.First().Component).Version);
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().Be(authorEmail);
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().BeNull();
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().Be(authorEmail);
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().BeNull();
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Should().BeNull();
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
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().BeNull();
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

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Name.Should().Be(authorName);
        ((NpmComponent)detectedComponents.First().Component).Author.Email.Should().Be(authorEmail);
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

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Should().BeNull();
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

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        AssertDetectedComponentCount(detectedComponents, 1);
        AssertNpmComponent(detectedComponents);
        ((NpmComponent)detectedComponents.First().Component).Author.Should().BeNull();
    }

    private static void AssertDetectedComponentCount(IEnumerable<DetectedComponent> detectedComponents, int expectedCount)
    {
        detectedComponents.Should().HaveCount(expectedCount);
    }

    private static void AssertNpmComponent(IEnumerable<DetectedComponent> detectedComponents)
    {
        detectedComponents.First().Component.Type.Should().Be(ComponentType.Npm);
    }
}
