#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComposerDetectorTests : BaseDetectorTest<ComposerComponentDetector>
{
    [TestInitialize]
    public void Initialize()
    {
        this.DetectorTestUtility.AddServiceMock(new Mock<IEnvironmentVariableService>());
        this.DetectorTestUtility.AddServiceMock(new Mock<ICommandLineInvocationService>());
    }

    [TestMethod]
    [DataRow("{\n// A manifest comment\n\"require\":{\"vendor/package\":\"v1.2.3\"}}")]
    [DataRow("{\"require\":{\"vendor/package\":\"v1.2.3\",},}")]
    [DataRow("{/* A manifest comment */\"require\":{\"vendor/package\":\"v1.2.3\",},}")]
    public async Task ManifestOnly_AllowsCommentsAndTrailingCommasAsync(string content)
    {
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.json", content).ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/package", "v1.2.3"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Lockfile_AllowsCommentsAndTrailingCommasAsync()
    {
        const string content = """
            {
              // Resolved packages
              "packages":[
                {"name":"vendor/app","version":"v1.2.3","require":{"vendor/child":"^2",},},
                {"name":"vendor/child","version":"2.1.0",},
              ],
            }
            """;
        var (result, recorder) = await this.DetectAsync(content);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id("vendor/app", "v1.2.3"), Id("vendor/child", "2.1.0")]);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "v1.2.3")).Should().Equal(Id("vendor/child", "2.1.0"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Lockfile_ReadsSiblingManifestForDirectRootsWithoutAdditionalLocationsOrInstalledLookupAsync()
    {
        var factory = this.WithManifest("""{/* Both packages are direct */"require":{"vendor/app":"^1","vendor/child":"^2",},}""");
        var files = new Mock<IFileUtilityService>(MockBehavior.Strict);
        var (_, recorder) = await this.DetectorTestUtility.AddServiceMock(files)
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/app","version":"1.2.3","require":{"vendor/child":"^2"}},{"name":"vendor/child","version":"2.1.0"}]}""")
            .ExecuteDetectorAsync();

        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/app", "1.2.3"), Id("vendor/child", "2.1.0")]);
        graph.GetDependenciesForComponent(Id("vendor/app", "1.2.3")).Should().Equal(Id("vendor/child", "2.1.0"));
        graph.GetAdditionalRelatedFiles().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
        factory.Verify(value => value.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(patterns => patterns.SequenceEqual(new[] { "composer.json" })), It.IsAny<ExcludeDirectoryPredicate>(), false), Times.Once);
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("{/* Installed inventory */\"packages\":[{\"name\":\"vendor/package\",\"version\":\"v1.2.3\",},],}")]
    [DataRow("[/* Legacy installed inventory */{\"name\":\"vendor/package\",\"version\":\"v1.2.3\",},]")]
    public async Task InstalledInventory_AllowsCommentsAndTrailingCommasAsync(string content)
    {
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), content);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/package":"^1"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/package", "v1.2.3"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(259)]
    [DataRow(260)]
    [DataRow(261)]
    [DataRow(4095)]
    [DataRow(4096)]
    [DataRow(4097)]
    [DataRow(32766)]
    [DataRow(32767)]
    [DataRow(32768)]
    [DataRow(65536)]
    [DataRow(1000000)]
    public void DirectoryNameExtraction_DoesNotApplyFilesystemLengthLimits(int pathLength)
    {
        const string fileName = "composer.json";
        var location = CreateLongFileLocation(pathLength, fileName);
        var expectedDirectory = location[..^(fileName.Length + 1)];

        location.Length.Should().Be(pathLength);
        Path.GetDirectoryName(location).Should().Be(expectedDirectory);
    }

    [TestMethod]
    [DataRow(261, false)]
    [DataRow(261, true)]
    [DataRow(4097, false)]
    [DataRow(4097, true)]
    [DataRow(32000, false)]
    [DataRow(32000, true)]
    public async Task LongCheckoutPaths_PreserveLocationsAndPreferSiblingLockfilesAsync(int pathLength, bool hasLockfile)
    {
        var fileName = hasLockfile ? "composer.lock" : "composer.json";
        var location = CreateLongFileLocation(pathLength, fileName);
        var directory = location[..^(fileName.Length + 1)];
        this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/package":"v9.0.0"}}""", fileLocation: Path.Combine(directory, "composer.json"));
        if (hasLockfile)
        {
            this.DetectorTestUtility.WithFile("composer.lock", """{"packages":[{"name":"vendor/package","version":"v1.2.3"}]}""", fileLocation: location);
        }

        var (result, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        location.Length.Should().Be(pathLength);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/package", hasLockfile ? "v1.2.3" : "v9.0.0"));
        recorder.GetDependencyGraphsByLocation().Keys.Should().Equal(location);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null, null, "vendor")]
    [DataRow(null, "dependencies", "dependencies")]
    [DataRow("", "dependencies", "dependencies")]
    [DataRow("env-vendor", "dependencies", "env-vendor")]
    [DataRow("env-vendor", null, "env-vendor")]
    [DataRow("absolute", "dependencies", "absolute")]
    [DataRow(null, "absolute", "absolute")]
    public async Task InstalledInventory_UsesEnvironmentThenManifestThenDefaultVendorDirectoryAsync(string environmentDirectory, string configuredDirectory, string expectedDirectory)
    {
        var absoluteDirectory = Path.Combine(Path.GetTempPath(), "composer-artifacts");
        environmentDirectory = environmentDirectory == "absolute" ? absoluteDirectory : environmentDirectory;
        configuredDirectory = configuredDirectory == "absolute" ? absoluteDirectory : configuredDirectory;
        var vendorDirectory = expectedDirectory == "absolute" ? absoluteDirectory : Path.Combine(Path.GetTempPath(), expectedDirectory);
        var environment = new Mock<IEnvironmentVariableService>();
        environment.Setup(value => value.GetEnvironmentVariable("COMPOSER_VENDOR_DIR")).Returns(environmentDirectory);
        var config = System.Text.Json.JsonSerializer.Serialize(new { config = new Dictionary<string, string> { ["vendor-dir"] = configuredDirectory } });
        var manifest = configuredDirectory is null
            ? """{"require":{"vendor/app":"^1"},"require-dev":{"vendor/tests":"^2"}}"""
            : config[..^1] + ""","require":{"vendor/app":"^1"},"require-dev":{"vendor/tests":"^2"}}""";
        const string installed = """
            {"packages":[
              {"name":"vendor/app","version":"v1.4.0","version_normalized":"1.4.0.0","require":{"vendor/child":"^3"}},
              {"name":"vendor/child","version":"3.1.0"},
              {"name":"vendor/tests","version":"2.2.0","require":{"vendor/child":"^3"}}
            ],"dev":true,"dev-package-names":["vendor/tests"]}
            """;
        var files = this.WithInstalledFile(vendorDirectory, installed);
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var (result, recorder) = await this.DetectorTestUtility.AddServiceMock(environment).AddServiceMock(logger)
            .WithFile("composer.json", manifest).ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            [Id("vendor/app", "v1.4.0"), Id("vendor/child", "3.1.0"), Id("vendor/tests", "2.2.0")]);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/app", "v1.4.0"), Id("vendor/tests", "2.2.0")]);
        graph.GetDependenciesForComponent(Id("vendor/app", "v1.4.0")).Should().Equal(Id("vendor/child", "3.1.0"));
        graph.GetDependenciesForComponent(Id("vendor/tests", "2.2.0")).Should().Equal(Id("vendor/child", "3.1.0"));
        graph.IsDevelopmentDependency(Id("vendor/app", "v1.4.0")).Should().BeFalse();
        graph.IsDevelopmentDependency(Id("vendor/child", "3.1.0")).Should().BeFalse();
        graph.IsDevelopmentDependency(Id("vendor/tests", "2.2.0")).Should().BeTrue();
        graph.GetAdditionalRelatedFiles().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
        files.Verify(value => value.MakeFileStream(Path.Combine(vendorDirectory, "composer", "installed.json")), Times.Once);
        logger.Verify(value => value.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
    }

    [TestMethod]
    public async Task InstalledInventory_LegacyArrayPreservesRawIdentitiesAndResolvesVirtualDependenciesAsync()
    {
        const string installed = """
            [{"name":" Vendor/App ","version":"dev-main","require":{"virtual/api":"^3"}},
             {"name":"vendor/provider","version":"v3.2.0","provide":{"virtual/api":"self.version"}}]
            """;
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), installed);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"dev-main"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id(" Vendor/App ", "dev-main"), Id("vendor/provider", "v3.2.0")]);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().Equal(Id(" Vendor/App ", "dev-main"));
        graph.GetDependenciesForComponent(Id(" Vendor/App ", "dev-main")).Should().Equal(Id("vendor/provider", "v3.2.0"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task InstalledInventory_IsNotDiscoveredWithoutAComposerFileAsync()
    {
        var files = this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), """{"packages":[{"name":"vendor/no-project","version":"1"}]}""");
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("{\"packages\":[]}")]
    public async Task EmptyInstalledInventory_DoesNotInventDeclaredPackagesAsync(string content)
    {
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), content);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/not-installed":"1.0.0"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{\"packages\":false}")]
    public async Task MalformedInstalledInventory_ReportsFailureAndFallsBackToExactDeclarationsAsync(string content)
    {
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), content);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/pinned":"1.2.3","vendor/ranged":"^2"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/pinned", "1.2.3"));
        recorder.GetSkippedComponents().Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("{\"config\":false}")]
    [DataRow("{\"config\":null}")]
    [DataRow("{\"config\":[]}")]
    [DataRow("{\"config\":{\"vendor-dir\":42}}")]
    [DataRow("{\"config\":{\"vendor-dir\":null}}")]
    [DataRow("{\"config\":{\"vendor-dir\":\"\"}}")]
    [DataRow("{\"config\":{\"vendor-dir\":\" \"}}")]
    [DataRow("{\"config\":{\"vendor-dir\":\"\\u0000\"}}")]
    public async Task InvalidVendorConfiguration_WarnsAndReadsDefaultVendorInventoryAsync(string content)
    {
        var location = Path.Combine(Path.GetTempPath(), "vendor", "composer", "installed.json");
        var files = this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), """{"packages":[{"name":"vendor/installed","version":"1.2.3"}]}""");
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var (_, recorder) = await this.DetectorTestUtility.AddServiceMock(logger).WithFile("composer.json", content).ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/installed", "1.2.3"));
        recorder.GetSkippedComponents().Should().BeEmpty();
        files.Verify(value => value.MakeFileStream(location), Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("using the default vendor directory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InvalidVendorEnvironmentPath_WarnsAndUsesDefaultRatherThanConfiguredDirectoryAsync()
    {
        var environment = new Mock<IEnvironmentVariableService>();
        environment.Setup(value => value.GetEnvironmentVariable("COMPOSER_VENDOR_DIR")).Returns("invalid\0path");
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), """{"packages":[{"name":"vendor/installed","version":"1.2.3"}]}""");
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var (_, recorder) = await this.DetectorTestUtility.AddServiceMock(environment).AddServiceMock(logger)
            .WithFile("composer.json", """{"config":{"vendor-dir":"dependencies"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/installed", "1.2.3"));
        recorder.GetSkippedComponents().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("using the default vendor directory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    [DataRow(false, "vendor")]
    [DataRow(true, "vendor")]
    [DataRow(false, "dependencies")]
    [DataRow(true, "dependencies")]
    public async Task Lockfile_SkipsVendorInputsAndNeverOpensInstalledMetadataAsync(bool lockFirst, string vendorDirectory)
    {
        this.WithManifest($$$"""{"config":{"vendor-dir":"{{{vendorDirectory}}}"}}""");
        var files = this.WithInstalledFile(Path.Combine(Path.GetTempPath(), vendorDirectory), "{");
        var nestedLocation = Path.Combine(Path.GetTempPath(), vendorDirectory, "vendor", "package", "composer.json");
        if (lockFirst)
        {
            this.DetectorTestUtility.WithFile("composer.lock", """{"packages":[{"name":"vendor/locked","version":"1.4.0"}]}""");
        }

        this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/not-installed":"9.0.0"}}""", fileLocation: nestedLocation);
        if (!lockFirst)
        {
            this.DetectorTestUtility.WithFile("composer.lock", """{"packages":[{"name":"vendor/locked","version":"1.4.0"}]}""");
        }

        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/locked", "1.4.0"));
        recorder.GetSkippedComponents().Should().BeEmpty();
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ParentLock_DoesNotSuppressAnIndependentNestedProjectAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/nested":"2.0.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), "nested-project", "composer.json"))
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/parent","version":"1.0.0"}]}""")
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id("vendor/parent", "1.0.0"), Id("vendor/nested", "2.0.0")]);
    }

    [TestMethod]
    public async Task InstalledInventory_SkipsNestedPackageManifestsAsync()
    {
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), """{"packages":[{"name":"vendor/app","version":"1.4.0"}]}""");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/not-installed":"9.0.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), "vendor", "vendor", "app", "composer.json"))
            .WithFile("composer.json", """{"require":{"vendor/app":"^1"}}""")
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/app", "1.4.0"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task MissingInstalledInventory_DoesNotSuppressNestedManifestFallbackAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/nested":"2.0.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), "vendor", "vendor", "app", "composer.json"))
            .WithFile("composer.json", """{"require":{"vendor/app":"^1"}}""")
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/nested", "2.0.0"));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ManifestOnly_RegistersExactDirectRequirementsWithProductionPrecedenceAsync()
    {
        const string manifest = """
            {"name":"vendor/project","version":"9.0.0",
             "require":{"php":"^8","vendor/runtime":"v1.2.3","vendor/shared":"2.0.0"},
             "require-dev":{"vendor/tests":"== 3.0.0-beta2","vendor/shared":"2.0.0"}}
            """;
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var (result, recorder) = await this.DetectorTestUtility
            .AddServiceMock(logger)
            .WithFile("composer.json", manifest)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            [Id("vendor/runtime", "v1.2.3"), Id("vendor/shared", "2.0.0"), Id("vendor/tests", "3.0.0-beta2")]);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().HaveCount(3);
        graph.IsDevelopmentDependency(Id("vendor/runtime", "v1.2.3")).Should().BeFalse();
        graph.IsDevelopmentDependency(Id("vendor/shared", "2.0.0")).Should().BeFalse();
        graph.IsDevelopmentDependency(Id("vendor/tests", "3.0.0-beta2")).Should().BeTrue();
        graph.GetDependenciesForComponent(Id("vendor/runtime", "v1.2.3")).Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("without a sibling composer.lock") && state.ToString().Contains("improve accuracy")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ManifestOnly_PreservesSourceNameAndVersionPrefixAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{" Vendor/Package ":"v1.2.3"}}""")
            .ExecuteDetectorAsync();

        var component = recorder.GetDetectedComponents().Should().ContainSingle().Which.Component.Should().BeOfType<ComposerComponent>().Which;
        component.Name.Should().Be(" Vendor/Package ");
        component.Version.Should().Be("v1.2.3");
        component.BaseId.Should().Be(" Vendor/Package  v1.2.3 - Composer");
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Lockfile_PreservesSourceFieldsWhileResolvingNormalizedNamesAndAliasesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":" Vendor/Root ","version":" v1.2.3 ","require":{"VENDOR/CHILD":"*","virtual/api":"^3"}},
              {"name":" Vendor/Child ","version":" dev-main ","provide":{"virtual/api":"self.version"},
               "extra":{"branch-alias":{"dev-main":"3.x-dev"}}}
            ]}
            """);

        var components = recorder.GetDetectedComponents().Select(component => component.Component).OfType<ComposerComponent>().ToDictionary(component => component.Id);
        components.Should().HaveCount(2);
        components[Id(" Vendor/Root ", " v1.2.3 ")].Name.Should().Be(" Vendor/Root ");
        components[Id(" Vendor/Root ", " v1.2.3 ")].Version.Should().Be(" v1.2.3 ");
        components[Id(" Vendor/Child ", " dev-main ")].Name.Should().Be(" Vendor/Child ");
        components[Id(" Vendor/Child ", " dev-main ")].Version.Should().Be(" dev-main ");
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id(" Vendor/Root ", " v1.2.3 ")).Should().Equal(Id(" Vendor/Child ", " dev-main "));
        graph.GetAllExplicitlyReferencedComponents().Should().Equal(Id(" Vendor/Root ", " v1.2.3 "));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("^1.0", false)]
    [DataRow("^1.2.3", false)]
    [DataRow("^v1.2.3", false)]
    [DataRow("^ 1.2.3", false)]
    [DataRow("^1.2.3", true)]
    [DataRow("~1.2", false)]
    [DataRow("~1.2.3", false)]
    [DataRow("~v1.2.3", false)]
    [DataRow("~ 1.2.3", false)]
    [DataRow("~1.2.3", true)]
    [DataRow("^1.2.3-beta2", false)]
    [DataRow("~1.2.3-beta2", true)]
    [DataRow("^", false)]
    [DataRow("~", false)]
    [DataRow("^1 || ^2", false)]
    [DataRow("~1 >=1.2", false)]
    [DataRow("*", false)]
    [DataRow("*", true)]
    [DataRow("1.2.*", false)]
    [DataRow("^1.2.*", false)]
    [DataRow("~1.2.*", false)]
    [DataRow(">=1 <2", false)]
    [DataRow(">1", false)]
    [DataRow("<2", false)]
    [DataRow("<=2", false)]
    [DataRow("!=1.5", false)]
    [DataRow("1.0 || 2.0", false)]
    [DataRow("1.0 - 2.0", false)]
    [DataRow("dev-main", false)]
    [DataRow("1.x-dev", false)]
    [DataRow("dev-main as 1.0.0", false)]
    public async Task ManifestOnly_UnresolvedConstraintsAreOmittedWithWarningsAsync(string constraint, bool isDevelopment)
    {
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var section = isDevelopment ? "require-dev" : "require";
        var content = $$$"""{"{{{section}}}":{"vendor/unresolved":"{{{constraint}}}","vendor/pinned":"1.2.3"}}""";
        var (_, recorder) = await this.DetectorTestUtility.AddServiceMock(logger).WithFile("composer.json", content).ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/pinned", "1.2.3"));
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().Equal(Id("vendor/pinned", "1.2.3"));
        graph.IsDevelopmentDependency(Id("vendor/pinned", "1.2.3")).Should().Be(isDevelopment);
        recorder.GetSkippedComponents().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("vendor/unresolved") && state.ToString().Contains(constraint) && state.ToString().Contains("this declaration is omitted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    [DataRow("composer.json")]
    [DataRow("COMPOSER.JSON")]
    [DataRow("Composer.Json")]
    public async Task ManifestFilenameMatching_IsCaseInsensitiveAsync(string fileName)
    {
        var (result, recorder) = await this.DetectorTestUtility
            .WithFile(fileName, """{"require":{"vendor/pinned":"1.2.3"}}""")
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/pinned", "1.2.3"));
        recorder.GetDependencyGraphsByLocation().Keys.Should().Equal(Path.Combine(Path.GetTempPath(), fileName));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false, "composer.lock", "composer.json")]
    [DataRow(true, "composer.lock", "composer.json")]
    [DataRow(false, "COMPOSER.LOCK", "COMPOSER.JSON")]
    [DataRow(true, "COMPOSER.LOCK", "COMPOSER.JSON")]
    [DataRow(false, "Composer.Lock", "Composer.Json")]
    [DataRow(true, "Composer.Lock", "Composer.Json")]
    public async Task ManifestAndLock_AlwaysUseLockedVersionsRegardlessOfDiscoveryOrderAsync(bool lockFirst, string lockName, string manifestName)
    {
        const string manifest = """{"require":{"vendor/app":"^1.0","vendor/child":"^2.0","vendor/not-installed":"2.0.0"}}""";
        const string locked = """{"packages":[{"name":"vendor/app","version":"1.4.0","require":{"vendor/child":"^2.0"}},{"name":"vendor/child","version":"2.1.0"}]}""";
        this.WithManifest(manifest, manifestName);
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(logger);
        if (lockFirst)
        {
            this.DetectorTestUtility.WithFile(lockName, locked).WithFile(manifestName, manifest);
        }
        else
        {
            this.DetectorTestUtility.WithFile(manifestName, manifest).WithFile(lockName, locked);
        }

        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id("vendor/app", "1.4.0"), Id("vendor/child", "2.1.0")]);
        var graphs = recorder.GetDependencyGraphsByLocation();
        graphs.Keys.Should().Equal(Path.Combine(Path.GetTempPath(), lockName));
        graphs.Values.Single().GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/app", "1.4.0"), Id("vendor/child", "2.1.0")]);
        graphs.Values.Single().GetAdditionalRelatedFiles().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("without a sibling composer.lock")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DirectoryGrouping_DistinguishesPathsByCaseAsync()
    {
        var manifestLocation = Path.Combine(Path.GetTempPath(), "ComposerProject", "composer.json");
        var lockLocation = Path.Combine(Path.GetTempPath(), "composerproject", "composer.lock");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/not-locked":"1.2.3"}}""", fileLocation: manifestLocation)
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/locked","version":"2.0.0"}]}""", fileLocation: lockLocation)
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id("vendor/not-locked", "1.2.3"), Id("vendor/locked", "2.0.0")]);
        recorder.GetDependencyGraphsByLocation().Keys.Should().BeEquivalentTo([manifestLocation, lockLocation]);
    }

    [TestMethod]
    public async Task LockInOtherDirectory_DoesNotSuppressManifestFallbackAsync()
    {
        var manifestLocation = Path.Combine(Path.GetTempPath(), "manifest-only", "composer.json");
        var lockLocation = Path.Combine(Path.GetTempPath(), "locked", "composer.lock");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/direct":"1.2.3"}}""", fileLocation: manifestLocation)
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/locked","version":"2.0.0"}]}""", fileLocation: lockLocation)
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo([Id("vendor/direct", "1.2.3"), Id("vendor/locked", "2.0.0")]);
        recorder.GetDependencyGraphsByLocation().Keys.Should().BeEquivalentTo([manifestLocation, lockLocation]);
    }

    [TestMethod]
    public async Task MalformedLock_DoesNotFallBackToManifestDeclarationsAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/not-locked":"1.2.3"}}""")
            .WithFile("composer.lock", "{")
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("""{"require":[]}""")]
    [DataRow("""{"require":{"vendor/invalid":42}}""")]
    [DataRow("""{"require":{"php-future-capability":42}}""")]
    public async Task ManifestOnly_MalformedDataReportsParseFailureAsync(string content)
    {
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", content).ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("package")]
    [DataRow("vendor/")]
    [DataRow("/package")]
    [DataRow(" ")]
    public async Task MissingPackageIdentity_ReportsParseFailureAsync(string name)
    {
        var content = $$$"""{"packages":[{"name":"{{{name}}}","version":"1.0"}]}""";
        var (_, recorder) = await this.DetectAsync(content);

        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("vendor--name/package---name", "release-next")]
    [DataRow("vendor./package__name", "custom-version#revision")]
    [DataRow("vendor_/package.-name", "2026.11.06-preview+vendor-build")]
    [DataRow("vendor/pack age", "vnext")]
    public async Task LockedIdentities_AreNotRestrictedToNameOrVersionGrammarAsync(string name, string version)
    {
        var content = $$$"""{"packages":[{"name":"{{{name}}}","version":"{{{version}}}"}]}""";
        var (_, recorder) = await this.DetectAsync(content);

        var component = recorder.GetDetectedComponents().Should().ContainSingle().Which.Component.Should().BeOfType<ComposerComponent>().Which;
        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.PackageUrl.Type.Should().Be("composer");
        component.PackageUrl.Version.Should().Be(version);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ProductionAndDevPackages_HaveAccurateFlagsAndEdgesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"Vendor/App","version":"v1.0.0","require":{"vendor/shared":"^2.0"}},
              {"name":"vendor/shared","version":"2.1.0"}
            ],"packages-dev":[
              {"name":"vendor/tests","version":"dev-main","require":{"vendor/shared":"^2.0"},
               "require-dev":{"vendor/uninstalled":"*"}}
            ]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(3);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id("Vendor/App", "v1.0.0")).Should().Equal(Id("vendor/shared", "2.1.0"));
        graph.GetDependenciesForComponent(Id("vendor/tests", "dev-main")).Should().Equal(Id("vendor/shared", "2.1.0"));
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("Vendor/App", "v1.0.0"), Id("vendor/tests", "dev-main")]);
        graph.IsDevelopmentDependency(Id("vendor/tests", "dev-main")).Should().BeTrue();
        graph.IsDevelopmentDependency(Id("vendor/shared", "2.1.0")).Should().BeFalse();
    }

    [TestMethod]
    public async Task DuplicateProductionAndDevEntries_MergeWithProductionPrecedenceAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[{"name":"vendor/shared","version":"1.0.0"}],
             "packages-dev":[
               {"name":"vendor/shared","version":"1.0.0","require":{"vendor/child":"*"}},
               {"name":"vendor/child","version":"1.0.0"}]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(2);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.IsDevelopmentDependency(Id("vendor/shared", "1.0.0")).Should().BeFalse();
        graph.IsDevelopmentDependency(Id("vendor/child", "1.0.0")).Should().BeTrue();
        graph.GetDependenciesForComponent(Id("vendor/shared", "1.0.0")).Should().Equal(Id("vendor/child", "1.0.0"));
    }

    [TestMethod]
    public async Task DifferentRawIdentities_AreRetainedAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"Vendor/Package","version":"v1.2.3"},
              {"name":"vendor/package","version":"v1.2.3"},
              {"name":"vendor/package","version":"1.2.3"}
            ]}
            """);

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            ["Vendor/Package v1.2.3 - Composer", "vendor/package v1.2.3 - Composer", "vendor/package 1.2.3 - Composer"]);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task VirtualProvideAndReplace_ResolveToActualPackagesOnlyAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1.0","require":{"psr/log-implementation":"^3","vendor/old":"^2"}},
              {"name":"vendor/logger","version":"9.0","provide":{"psr/log-implementation":"3.0"}},
              {"name":"vendor/new","version":"v2.1.0","replace":{"vendor/old":"self.version"}}
            ]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(3);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id("vendor/app", "1.0")).Should().BeEquivalentTo([Id("vendor/logger", "9.0"), Id("vendor/new", "v2.1.0")]);
        graph.GetAllExplicitlyReferencedComponents().Should().Equal(Id("vendor/app", "1.0"));
    }

    [TestMethod]
    public async Task VirtualProviders_AreFilteredByDeclaredVersionNotProviderVersionAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/one","version":"2","provide":{"virtual/api":"1.0"}},
              {"name":"vendor/two","version":"99","provide":{"virtual/api":"2.0"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().Equal(Id("vendor/two", "99"));
    }

    [TestMethod]
    public async Task AmbiguousProviders_DoNotFabricateEdgesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"*"}},
              {"name":"vendor/one","version":"1","provide":{"virtual/api":"*"}},
              {"name":"vendor/two","version":"1","replace":{"virtual/api":"*"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
        recorder.GetDetectedComponents().Should().HaveCount(3);
    }

    [TestMethod]
    public async Task UnsupportedProviderConstraint_DoesNotCreateFalseUniqueProviderAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/known","version":"1","provide":{"virtual/api":"2"}},
              {"name":"vendor/unknown","version":"1","provide":{"virtual/api":"unsupported"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
    }

    [TestMethod]
    public async Task ActualPackage_TakesPrecedenceOverVirtualProviderAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"vendor/api":"*"}},
              {"name":"vendor/api","version":"2"},
              {"name":"vendor/provider","version":"3","provide":{"vendor/api":"*"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().Equal(Id("vendor/api", "2"));
    }

    [TestMethod]
    public async Task DirectPackageAndManifestInlineAliases_KeepLockedSourceIdentityAsync()
    {
        this.WithManifest("""{"require":{"vendor/app":"*","vendor/branch":"dev-main as 1.0.0"}}""");
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"vendor/branch":"dev-main as 1.0.0"}},
              {"name":"vendor/branch","version":"dev-main"}
            ]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(2);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id("vendor/app", "1")).Should().Equal(Id("vendor/branch", "dev-main"));
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/app", "1"), Id("vendor/branch", "dev-main")]);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("provide")]
    [DataRow("replace")]
    public async Task VirtualInlineAliases_CompareSourceWithoutInventingRhsEdgesAsync(string section)
    {
        var content = $$$"""
            {"packages":[
              {"name":"vendor/source","version":"1","require":{"virtual/source":"dev-main as 1.0.0"}},
              {"name":"vendor/numeric","version":"1","require":{"virtual/alias":"^1"}},
              {"name":"vendor/provider","version":"9","{{{section}}}":{"virtual/source":"dev-main","virtual/alias":"dev-main as 1.0.0"}}
            ]}
            """;
        var (_, recorder) = await this.DetectAsync(content);

        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id("vendor/source", "1")).Should().Equal(Id("vendor/provider", "9"));
        graph.GetDependenciesForComponent(Id("vendor/numeric", "1")).Should().BeEmpty();
        recorder.GetDetectedComponents().Should().HaveCount(3);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("dev-main", false)]
    [DataRow("^1", true)]
    public async Task ManifestInlineAliasVirtualClaim_UsesSourceAsync(string requirement, bool expectProvider)
    {
        this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"*"},"provide":{"virtual/api":"dev-main as 1.0.0"}}""");
        var content = $$$"""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"{{{requirement}}}"}},
              {"name":"vendor/provider","version":"1","provide":{"virtual/api":"dev-main || ^1"}}
            ]}
            """;
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), content);
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var dependencies = recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"));
        dependencies.Should().BeEquivalentTo(expectProvider ? [Id("vendor/provider", "1")] : []);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task PlatformRequirements_AreExcludedButNamespacedComposerPackagesRemainAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{
                "php":"^8","ext-json":"*","lib-pcre":"*","composer-plugin-api":"^2",
                "composer-runtime-api":"^2","composer/installers":"*","ext-vendor/real":"*"}},
              {"name":"composer/installers","version":"2"},
              {"name":"ext-vendor/real","version":"3"}
            ]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(3);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().BeEquivalentTo([Id("composer/installers", "2"), Id("ext-vendor/real", "3")]);
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("composer.lock", """{"packages":[{"name":"vendor/app","version":"1","require":{"php-future-capability":"^9"}}]}""")]
    [DataRow("composer.json", """{"require":{"php-future-capability":"^9","vendor/app":"1"}}""")]
    [DataRow("composer.json", """{"require-dev":{"php-future-capability":"^9","vendor/app":"1"}}""")]
    public async Task UnnamespacedRequirements_AreDebugLoggedWithoutReportingFailuresAsync(string fileName, string content)
    {
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        var (result, recorder) = await this.DetectorTestUtility.AddServiceMock(logger).WithFile(fileName, content).ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal(Id("vendor/app", "1"));
        recorder.GetSkippedComponents().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("Skipping Composer dependency php-future-capability")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Aliases_PreserveLockedIdentityAndResolveSelfVersionClaimsAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/branch","version":"dev-main","provide":{"virtual/api":"self.version"},
               "extra":{"branch-alias":{"dev-main":"2.x-dev"}}}
            ],"aliases":[{"package":"vendor/branch","version":"dev-main","alias":"2.x-dev"}]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(2);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().Equal(Id("vendor/branch", "dev-main"));
    }

    [TestMethod]
    public async Task LockAliases_ResolveNormalizedVersionsWithoutBranchMetadataAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":" Vendor--Name/Provider---Name ","version":" v1.0.0 ","provide":{"virtual/api":"self.version"}}
            ],"aliases":[{"package":"vendor--name/provider---name","version":"1.0.0.0","alias":"2.x-dev"}]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().Equal(Id(" Vendor--Name/Provider---Name ", " v1.0.0 "));
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("vendor/other", "1.0", true)]
    [DataRow(" Vendor/Provider ", "3.0", true)]
    [DataRow(" Vendor/Provider ", " v1.0.0 ", false)]
    public async Task LockAliases_RequireUniqueNameAndVersionMatchesAsync(string otherName, string otherVersion, bool resolvesAlias)
    {
        var logger = new Mock<ILogger<ComposerComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(logger);
        var (_, recorder) = await this.DetectAsync($$$"""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/provider","version":"1.0","provide":{"virtual/api":"self.version"}},
              {"name":"{{{otherName}}}","version":"{{{otherVersion}}}","provide":{"virtual/api":"self.version"}}
            ],"aliases":[{"package":" VENDOR/PROVIDER ","version":"1.0.0.0","alias":" 2.x-dev "}]}
            """);

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            [Id("vendor/app", "1"), Id("vendor/provider", "1.0"), Id(otherName, otherVersion)]);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().BeEquivalentTo(resolvesAlias ? [Id("vendor/provider", "1.0")] : []);
        recorder.GetSkippedComponents().Should().BeEmpty();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("does not uniquely identify an installed package")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            resolvesAlias ? Times.Never() : Times.Once());
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{"package":42,"version":"dev-main","alias":"2.x-dev"}""")]
    [DataRow("""{"package":" ","version":"dev-main","alias":"2.x-dev"}""")]
    [DataRow("""{"package":"vendor/branch","version":null,"alias":"2.x-dev"}""")]
    [DataRow("""{"package":"vendor/branch","version":"dev-main","alias":false}""")]
    [DataRow("""{"package":"vendor/branch","version":"dev-main","alias":" "}""")]
    public async Task MalformedLockAliases_ReportFailureAndRetainValidAliasesAsync(string invalidAlias)
    {
        var (_, recorder) = await this.DetectAsync($$$"""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/branch","version":"dev-main","provide":{"virtual/api":"self.version"}}
            ],"aliases":[{{{invalidAlias}}},{"package":"vendor/branch","version":"dev-main","alias":"2.x-dev"}]}
            """);

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            [Id("vendor/app", "1"), Id("vendor/branch", "dev-main")]);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().Equal(Id("vendor/branch", "dev-main"));
        recorder.GetSkippedComponents().Should().Equal("aliases[0]");
    }

    [TestMethod]
    [DataRow("null", "packages[1].extra.branch-alias")]
    [DataRow("[]", "packages[1].extra.branch-alias")]
    [DataRow("""{"dev-main":null}""", "packages[1].extra.branch-alias.dev-main")]
    [DataRow("""{"dev-main":42}""", "packages[1].extra.branch-alias.dev-main")]
    [DataRow("""{"dev-main":" "}""", "packages[1].extra.branch-alias.dev-main")]
    public async Task MalformedBranchAliases_ReportFailureWithoutDroppingLockedPackagesAsync(string aliases, string failureLocation)
    {
        var (_, recorder) = await this.DetectAsync($$$"""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/branch","version":"dev-main","provide":{"virtual/api":"self.version"},
               "extra":{"branch-alias":{{{aliases}}}}}
            ]}
            """);

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            [Id("vendor/app", "1"), Id("vendor/branch", "dev-main")]);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1"))
            .Should().BeEmpty();
        recorder.GetSkippedComponents().Should().Equal(failureLocation);
    }

    [TestMethod]
    public async Task ConflictingDuplicateDeclarations_ReportFailureWithoutFabricatedVirtualEdgesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/provider","version":"1","provide":{"virtual/api":"1"}}
            ],"packages-dev":[
              {"name":"vendor/provider","version":"1","provide":{"virtual/api":"2"}},
              {"name":"vendor/provider","version":"1","provide":{"virtual/api":"2"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
        recorder.GetDetectedComponents().Should().HaveCount(2);
        recorder.GetSkippedComponents().Should().HaveCount(1);
    }

    [TestMethod]
    public async Task ConflictingDuplicateDependencyNames_ReportFailuresAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/app","version":"1","require":{
                "invalid":"*","vendor/bad/name":"*","vendor/api":"1","VENDOR/API":"2","vendor/api":"2"}},
              {"name":"vendor/api","version":"2"}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
        recorder.GetSkippedComponents().Should().Equal("packages[0].require.vendor/api");
    }

    [TestMethod]
    public async Task SourceCycles_HaveCandidateRootsWithoutInventedEdgesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/a","version":"1","require":{"vendor/b":"*","vendor/a":"*"}},
              {"name":"vendor/b","version":"1","require":{"vendor/a":"*","vendor/c":"*"}},
              {"name":"vendor/c","version":"1"}
            ]}
            """);

        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/a", "1"), Id("vendor/b", "1")]);
        graph.GetDependenciesForComponent(Id("vendor/a", "1")).Should().Equal(Id("vendor/b", "1"));
        graph.GetExplicitReferencedDependencyIds(Id("vendor/c", "1")).Should().BeEquivalentTo([Id("vendor/a", "1"), Id("vendor/b", "1")]);
    }

    [TestMethod]
    public async Task ReachableCycles_AreNotRootsAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor/root","version":"1","require":{"vendor/a":"*"}},
              {"name":"vendor/a","version":"1","require":{"vendor/b":"*"}},
              {"name":"vendor/b","version":"1","require":{"vendor/a":"*"}}
            ]}
            """);

        recorder.GetDependencyGraphsByLocation().Values.Single().GetAllExplicitlyReferencedComponents().Should().Equal(Id("vendor/root", "1"));
    }

    [TestMethod]
    public async Task Manifest_KeepsDirectDependenciesEvenWhenAlsoTransitiveAsync()
    {
        this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/a":"*","vendor/b":"*"}}""");
        const string installed = """
            {"packages":[
              {"name":"vendor/a","version":"1","require":{"vendor/b":"*"}},
              {"name":"vendor/b","version":"1"}
            ]}
            """;
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), installed);
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDependencyGraphsByLocation().Values.Single().GetAllExplicitlyReferencedComponents()
            .Should().BeEquivalentTo([Id("vendor/a", "1"), Id("vendor/b", "1")]);
    }

    [TestMethod]
    public async Task Manifest_ProvidedVirtualDependencyDoesNotBecomePackageEdgeAsync()
    {
        this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"*"},"provide":{"virtual/api":"1"}}""");
        const string installed = """
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"*"}},
              {"name":"vendor/provider","version":"1","provide":{"virtual/api":"1"}}
            ]}
            """;
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), installed);
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
        graph.GetAllExplicitlyReferencedComponents().Should().Equal(Id("vendor/app", "1"));
    }

    [TestMethod]
    [DataRow("""{"require":{"vendor/app":"*"},"version":"dev-main","extra":{"branch-alias":{"dev-main":"2.x-dev"}},"provide":{"virtual/api":"self.version"}}""")]
    [DataRow("""{"require":{"vendor/app":"*"},"provide":{"virtual/api":"self.version"}}""")]
    public async Task Manifest_SelfVersionClaimsNeverGuessAnInstalledProviderAsync(string manifest)
    {
        this.DetectorTestUtility.WithFile("composer.json", manifest);
        const string installed = """
            {"packages":[
              {"name":"vendor/app","version":"1","require":{"virtual/api":"^2"}},
              {"name":"vendor/provider","version":"2","provide":{"virtual/api":"2"}}
            ]}
            """;
        this.WithInstalledFile(Path.Combine(Path.GetTempPath(), "vendor"), installed);
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"packages":[]}""")]
    [DataRow("""{"packages-dev":[]}""")]
    [DataRow("""{"packages":[],"packages-dev":[]}""")]
    public async Task MissingOrEmptySections_AreValidAsync(string content)
    {
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", content).ExecuteDetectorAsync();
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("""{"packages":null}""")]
    [DataRow("""{"packages":{}}""")]
    public async Task MalformedDocumentsOrSections_ReportParseFailuresAsync(string content)
    {
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", content).ExecuteDetectorAsync();
        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task MalformedEntries_ReportFailuresAndRetainValidPackagesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[null,{},{"name":"vendor/no-version"},{"name":"vendor/bad","version":42},
              {"name":"not-namespaced","version":"1"},{"name":"vendor/empty","version":" "},
              {"name":"vendor/valid","version":"1","require":{"vendor/child":false}},
              {"name":"vendor/valid2","version":"2","provide":[]}]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(2);
        recorder.GetSkippedComponents().Should().HaveCount(8);
    }

    [TestMethod]
    public async Task PackageNamesOutsideLegacySchema_AreDetectedAndLinkedAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[
              {"name":"vendor-/package","version":"1"},
              {"name":"vendor/package.","version":"1"},
              {"name":"vendor/package---name","version":"1"},
              {"name":"vendor--name/package","version":"1"},
              {"name":" Vendor/Package--Name ","version":" v1.2.3 "},
              {"name":"vendor/root","version":"2","require":{"vendor/package--name":"*","vendor/package---name":"*"}}
            ]}
            """);

        recorder.GetDetectedComponents().Should().HaveCount(6);
        recorder.GetSkippedComponents().Should().BeEmpty();
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/root", "2"))
            .Should().BeEquivalentTo([Id(" Vendor/Package--Name ", " v1.2.3 "), Id("vendor/package---name", "1")]);
    }

    [TestMethod]
    public async Task MalformedSiblingManifest_ReportsFailureAndFallsBackToLockedGraphRootsAsync()
    {
        this.WithManifest("{");
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", """{"packages":[{"name":"vendor/a","version":"1"}]}""").ExecuteDetectorAsync();
        recorder.GetSkippedComponents().Should().ContainSingle();
        recorder.GetDependencyGraphsByLocation().Values.Single().GetAllExplicitlyReferencedComponents().Should().Equal(Id("vendor/a", "1"));
    }

    [TestMethod]
    public async Task DifferentLockedVersions_AreRetainedWithoutAmbiguousNameEdgesAsync()
    {
        var (_, recorder) = await this.DetectAsync("""
            {"packages":[{"name":"vendor/app","version":"1","require":{"vendor/shared":"*"}},{"name":"vendor/shared","version":"1"}],
             "packages-dev":[{"name":"vendor/shared","version":"2"}]}
            """);
        recorder.GetDetectedComponents().Should().HaveCount(3);
        recorder.GetDependencyGraphsByLocation().Values.Single().GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
    }

    [TestMethod]
    public async Task MultipleLockfiles_KeepDependencyGraphsIsolatedAsync()
    {
        var first = Path.Combine(Path.GetTempPath(), "first", "composer.lock");
        var second = Path.Combine(Path.GetTempPath(), "second", "composer.lock");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/app","version":"1","require":{"vendor/child":"*"}},{"name":"vendor/child","version":"1"}]}""", fileLocation: first)
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/app","version":"1"},{"name":"vendor/child","version":"2"}]}""", fileLocation: second)
            .ExecuteDetectorAsync();

        var graphs = recorder.GetDependencyGraphsByLocation();
        graphs.Should().HaveCount(2);
        graphs[first].GetDependenciesForComponent(Id("vendor/app", "1")).Should().Equal(Id("vendor/child", "1"));
        graphs[second].GetDependenciesForComponent(Id("vendor/app", "1")).Should().BeEmpty();
        graphs[second].GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("vendor/app", "1"), Id("vendor/child", "2")]);
    }

    [TestMethod]
    public async Task LocalFixture_ProducesExpectedOfflineGraphAsync()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Composer");
        this.WithManifest(await File.ReadAllTextAsync(Path.Combine(directory, "composer.json")));
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", await File.ReadAllTextAsync(Path.Combine(directory, "composer.lock"))).ExecuteDetectorAsync();
        recorder.GetDetectedComponents().Should().HaveCount(5);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo([Id("example/application-library", "v1.2.0"), Id("example/test-runner", "dev-main")]);
        graph.GetDependenciesForComponent(Id("example/application-library", "v1.2.0"))
            .Should().BeEquivalentTo([Id("example/logger", "3.1.0"), Id("example/replacement", "1.4.0"), Id("composer/installers", "v2.3.0")]);
        graph.IsDevelopmentDependency(Id("example/test-runner", "dev-main")).Should().BeTrue();
        graph.GetAdditionalRelatedFiles().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    private static string CreateLongFileLocation(int pathLength, string fileName)
    {
        var path = new StringBuilder(pathLength).Append(Path.GetPathRoot(Path.GetTempPath()));
        var directoryLength = pathLength - fileName.Length - 1;
        while (path.Length < directoryLength)
        {
            var remaining = directoryLength - path.Length;
            if (remaining > 101)
            {
                path.Append('d', 100).Append(Path.DirectorySeparatorChar);
            }
            else
            {
                path.Append('d', remaining);
            }
        }

        return path.Append(Path.DirectorySeparatorChar).Append(fileName).ToString();
    }

    private static string Id(string name, string version) => new ComposerComponent(name, version).Id;

    private Mock<IFileUtilityService> WithInstalledFile(string vendorDirectory, string content)
    {
        var location = Path.Combine(vendorDirectory, "composer", "installed.json");
        var files = new Mock<IFileUtilityService>();
        files.Setup(value => value.Exists(location)).Returns(true);
        files.Setup(value => value.MakeFileStream(location)).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
        this.DetectorTestUtility.AddServiceMock(files);
        return files;
    }

    private Task<(IndividualDetectorScanResult ScanResult, IComponentRecorder ComponentRecorder)> DetectAsync(string content) =>
        this.DetectorTestUtility.WithFile("composer.lock", content).ExecuteDetectorAsync();

    private Mock<IComponentStreamEnumerableFactory> WithManifest(string content, string fileName = "composer.json")
    {
        var factory = new Mock<IComponentStreamEnumerableFactory>();
        var file = new Mock<IComponentStream>();
        file.SetupGet(value => value.Location).Returns(Path.Combine(Path.GetTempPath(), fileName));
        file.SetupGet(value => value.Stream).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
        IEnumerable<IComponentStream> files = [file.Object];
        factory.Setup(value => value.GetComponentStreams(It.Is<DirectoryInfo>(directory => directory.FullName == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)), It.Is<IEnumerable<string>>(patterns => patterns.SequenceEqual(new[] { "composer.json" })), It.IsAny<ExcludeDirectoryPredicate>(), false))
            .Returns(files);
        this.DetectorTestUtility.AddServiceMock(factory);
        return factory;
    }
}
