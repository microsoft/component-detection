#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
public class ComposerPlatformTests : BaseDetectorTest<ComposerComponentDetector>
{
    private Mock<ICommandLineInvocationService> commands;
    private Mock<ILogger<ComposerComponentDetector>> logger;

    [TestInitialize]
    public void Initialize()
    {
        this.commands = new Mock<ICommandLineInvocationService>(MockBehavior.Strict);
        this.commands.Setup(value => value.IsCommandLineExecution()).Returns(true);
        this.logger = new Mock<ILogger<ComposerComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(new Mock<IEnvironmentVariableService>()).AddServiceMock(this.commands).AddServiceMock(this.logger);
    }

    [TestMethod]
    public async Task ComposerFiles_RequestPlatformInventoryPerNonVendorDirectoryWithoutChangingPackageGraphsAsync()
    {
        this.WithOutput("""
            {"platform":[
              {"name":"php","version":"8.4.27","direct-dependency":true},
              {"name":"ext-openssl","version":"8.4.27","direct-dependency":false},
              {"name":"lib-openssl","version":"3.5.0"},
              {"name":"composer","version":"2.11.1"},
              {"name":"hhvm","version":"4.172.0"},
              {"name":"php-64bit","version":"8.4.27"},
              {"name":"composer-plugin-api","version":"2.9.0"},
              {"name":"composer-runtime-api","version":"2.2.2"},
              {"name":"vendor/not-platform","version":"1.0"}
            ]}
            """);
        var rootLocation = Path.Combine(Path.GetTempPath(), "composer.lock");
        var childLocation = Path.Combine(Path.GetTempPath(), "child", "composer.json");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/child":"1.2.3"}}""", fileLocation: childLocation)
            .WithFile("composer.json", """{"require":{"vendor/noise":"9.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), "vendor", "package", "composer.json"))
            .WithFile("composer.json", """{"require":{"vendor/app":"^1","php":"^8"}}""")
            .WithFile("composer.lock", """{"packages":[{"name":"vendor/app","version":"1.0.0"}]}""", fileLocation: rootLocation)
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(
            ["php 8.4.27 - PhpSdk", "ext-openssl 8.4.27 - PhpSdk", "lib-openssl 3.5.0 - PhpSdk", "composer 2.11.1 - PhpSdk", "hhvm 4.172.0 - PhpSdk", "vendor/app 1.0.0 - Composer", "vendor/child 1.2.3 - Composer"]);
        var graphs = recorder.GetDependencyGraphsByLocation();
        graphs[rootLocation].GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo(["php 8.4.27 - PhpSdk", "vendor/app 1.0.0 - Composer"]);
        recorder.GetDetectedComponents().Where(component => component.Component is PhpSdkComponent).Should()
            .OnlyContain(component => graphs[rootLocation].IsDevelopmentDependency(component.Component.Id) == true);
        graphs[rootLocation].IsDevelopmentDependency("vendor/app 1.0.0 - Composer").Should().BeFalse();
        graphs[rootLocation].GetDependenciesForComponent("vendor/app 1.0.0 - Composer").Should().BeEmpty();
        graphs[childLocation].GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo(["php 8.4.27 - PhpSdk", "vendor/child 1.2.3 - Composer"]);
        graphs[childLocation].IsDevelopmentDependency("php 8.4.27 - PhpSdk").Should().BeTrue();
        graphs[childLocation].IsDevelopmentDependency("vendor/child 1.2.3 - Composer").Should().BeFalse();
        recorder.GetSkippedComponents().Should().BeEmpty();
        string[] expectedCandidates = ["composer.cmd", "composer.bat"];
        string[] expectedArguments = ["--no-plugins", "--no-scripts", "--no-interaction", "--no-ansi", "show", "--platform", "--format=json"];
        foreach (var projectDirectory in new[] { Path.GetDirectoryName(rootLocation), Path.GetDirectoryName(childLocation) })
        {
            this.commands.Verify(
                value => value.ExecuteCommandAsync(
                    "composer",
                    It.Is<IEnumerable<string>>(candidates => candidates.SequenceEqual(expectedCandidates)),
                    It.Is<DirectoryInfo>(directory => directory.FullName == projectDirectory),
                    It.Is<CancellationToken>(token => token.CanBeCanceled),
                    It.Is<string[]>(arguments => arguments.SequenceEqual(expectedArguments))),
                Times.Once);
        }

        this.commands.Verify(value => value.IsCommandLineExecution(), Times.Exactly(2));
        this.commands.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LockfileWithoutManifest_InfersPackageRootsWithoutPromotingPlatformEntriesAsync()
    {
        this.WithOutput("""
            {"platform":[
              {"name":"php","version":"8.4.27","direct-dependency":true},
              {"name":"ext-json","version":"8.4.27","direct-dependency":false}
            ]}
            """);
        const string lockContent = """
            {"packages":[
              {"name":"vendor/app","version":"1.0.0","require":{"vendor/child":"^2"}},
              {"name":"vendor/child","version":"2.0.0"}
            ]}
            """;
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", lockContent).ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var graph = recorder.GetDependencyGraphsByLocation().Values.Single();
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo(
            ["php 8.4.27 - PhpSdk", "vendor/app 1.0.0 - Composer"]);
        graph.GetDependenciesForComponent("vendor/app 1.0.0 - Composer").Should().Equal("vendor/child 2.0.0 - Composer");
        graph.GetDependenciesForComponent("ext-json 8.4.27 - PhpSdk").Should().BeEmpty();
        recorder.GetSkippedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Projects_RecordDifferentPlatformVersionsAndDirectReferencesAsync()
    {
        this.WithOutput("""{"platform":[{"name":"php","version":"8.4.27","direct-dependency":true},{"name":"ext-json","version":"8.4.27","direct-dependency":false}]}""");
        var rootLocation = Path.Combine(Path.GetTempPath(), "composer.json");
        var childLocation = Path.Combine(Path.GetTempPath(), "child", "composer.json");
        this.commands.Setup(value => value.ExecuteCommandAsync(
                "composer",
                It.IsAny<IEnumerable<string>>(),
                It.Is<DirectoryInfo>(directory => directory.FullName == Path.GetDirectoryName(childLocation)),
                It.IsAny<CancellationToken>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = """{"platform":[{"name":"php","version":"8.3.15","direct-dependency":false},{"name":"ext-json","version":"8.4.27","direct-dependency":true}]}""",
            });
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", "{}", fileLocation: childLocation)
            .WithFile("composer.json", "{}", fileLocation: rootLocation)
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should()
            .BeEquivalentTo(["php 8.4.27 - PhpSdk", "php 8.3.15 - PhpSdk", "ext-json 8.4.27 - PhpSdk"]);
        var graphs = recorder.GetDependencyGraphsByLocation();
        graphs[rootLocation].GetAllExplicitlyReferencedComponents().Should().Equal("php 8.4.27 - PhpSdk");
        graphs[childLocation].GetAllExplicitlyReferencedComponents().Should().Equal("ext-json 8.4.27 - PhpSdk");
        graphs[rootLocation].IsDevelopmentDependency("php 8.4.27 - PhpSdk").Should().BeTrue();
        graphs[childLocation].IsDevelopmentDependency("php 8.3.15 - PhpSdk").Should().BeTrue();
        graphs[rootLocation].IsDevelopmentDependency("ext-json 8.4.27 - PhpSdk").Should().BeTrue();
        graphs[childLocation].IsDevelopmentDependency("ext-json 8.4.27 - PhpSdk").Should().BeTrue();
        this.commands.Verify(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()), Times.Exactly(2));
    }

    [TestMethod]
    [DataRow("vendor", false)]
    [DataRow("dependencies", false)]
    [DataRow("environment-vendor", true)]
    public async Task VendorDirectories_DoNotQueryPlatformWhenParentInventoryIsMissingAsync(string vendorDirectory, bool fromEnvironment)
    {
        this.WithOutput("""{"platform":[{"name":"php","version":"8.4.27"}]}""");
        var environment = new Mock<IEnvironmentVariableService>();
        if (fromEnvironment)
        {
            environment.Setup(value => value.GetEnvironmentVariable("COMPOSER_VENDOR_DIR")).Returns(vendorDirectory);
        }

        this.DetectorTestUtility.AddServiceMock(environment);
        var configuredVendorDirectory = fromEnvironment ? "ignored-vendor" : vendorDirectory;
        var rootLocation = Path.Combine(Path.GetTempPath(), "composer.json");
        var vendorLocation = Path.Combine(Path.GetTempPath(), vendorDirectory, "package", "composer.json");
        var independentLocation = Path.Combine(Path.GetTempPath(), vendorDirectory + "-project", "composer.json");
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/nested":"2.0"}}""", fileLocation: vendorLocation)
            .WithFile("composer.json", """{"require":{"vendor/independent":"3.0"}}""", fileLocation: independentLocation)
            .WithFile("composer.json", $$$"""{"config":{"vendor-dir":"{{{configuredVendorDirectory}}}"},"require":{"vendor/app":"1.0"}}""", fileLocation: rootLocation)
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should()
            .BeEquivalentTo(["php 8.4.27 - PhpSdk", "vendor/app 1.0 - Composer", "vendor/nested 2.0 - Composer", "vendor/independent 3.0 - Composer"]);
        recorder.GetDependencyGraphsByLocation()[vendorLocation].GetAllExplicitlyReferencedComponents().Should().Equal("vendor/nested 2.0 - Composer");
        this.commands.Verify(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()), Times.Exactly(2));
        this.commands.Verify(
            value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.Is<DirectoryInfo>(directory => directory.FullName == Path.GetDirectoryName(vendorLocation)), It.IsAny<CancellationToken>(), It.IsAny<string[]>()),
            Times.Never);
    }

    [TestMethod]
    [DataRow("vendor")]
    [DataRow("VeNdOr")]
    public async Task VendorOnlyInput_DoesNotQueryPlatformWithoutAParentProjectAsync(string vendorDirectory)
    {
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/package":"1.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), vendorDirectory, "package", "composer.json"))
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("vendor/package 1.0 - Composer");
        this.commands.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("""{"name":"php","version":"8.4.27","direct-dependency":true}""", true, false)]
    [DataRow("""{"name":"php","version":"8.4.27","direct-dependency":false}""", false, false)]
    [DataRow("""{"name":"php","version":"8.4.27"}""", false, false)]
    [DataRow("""{"name":"php","version":"8.4.27","direct-dependency":"true"}""", false, true)]
    [DataRow("""{"name":"php","version":"8.4.27","direct-dependency":null}""", false, true)]
    [DataRow("""{"name":"php","version":"8.4.27","direct-dependency":1}""", false, true)]
    public async Task PlatformDirectDependency_UsesComposerMetadataWithoutDroppingInventoryAsync(string entry, bool explicitlyReferenced, bool invalidMetadata)
    {
        this.WithOutput($$"""{"platform":[{{entry}}]}""");
        var location = Path.Combine(Path.GetTempPath(), "composer.json");
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.json", "{}", fileLocation: location).ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("php 8.4.27 - PhpSdk");
        var graph = recorder.GetDependencyGraphsByLocation()[location];
        string[] expectedExplicitReferences = explicitlyReferenced ? ["php 8.4.27 - PhpSdk"] : [];
        graph.GetAllExplicitlyReferencedComponents().Should().BeEquivalentTo(expectedExplicitReferences);
        graph.IsDevelopmentDependency("php 8.4.27 - PhpSdk").Should().BeTrue();
        recorder.GetSkippedComponents().Should().BeEmpty();
        this.VerifyWarning("invalid direct-dependency", invalidMetadata ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task NoComposerFile_DoesNotQueryTheEnvironmentAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        this.commands.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LibraryHost_DoesNotQueryTheEnvironmentAsync()
    {
        this.commands.Setup(value => value.IsCommandLineExecution()).Returns(false);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"1.0"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("vendor/app 1.0 - Composer");
        this.commands.Verify(value => value.IsCommandLineExecution(), Times.Once);
        this.commands.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task InvalidProjectJson_DoesNotQueryTheEnvironmentAsync()
    {
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", "{").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Should().BeEmpty();
        recorder.GetSkippedComponents().Should().ContainSingle();
        this.commands.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task PlatformOverrides_AreNotReportedAsInstalledVersionsAsync()
    {
        this.WithOutput("""
            {"platform":[
              {"name":"php","version":"7.4.0","description":"Package overridden via config.platform, actual: 8.4.27"},
              {"name":"ext-json","version":"8.4.27","description":"The json PHP extension. Package disabled via config.platform"},
              {"name":"composer","version":"2.11.1"}
            ]}
            """);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"config":{"platform":{"php":"7.4.0"}},"require":{"php":"^7"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("composer 2.11.1 - PhpSdk");
        this.VerifyWarning("config.platform", Times.Never());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"platform\":{}}")]
    public async Task InvalidOutput_WarnsAndPreservesPackageDetectionAsync(string output)
    {
        this.WithOutput(output);
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.lock", """{"packages":[{"name":"vendor/app","version":"1.0"}]}""").ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("vendor/app 1.0 - Composer");
        recorder.GetSkippedComponents().Should().BeEmpty();
        this.VerifyWarning("Composer platform inventory", Times.Once());
    }

    [TestMethod]
    public async Task InvalidEntries_WarnAndRetainValidEnvironmentComponentsAsync()
    {
        this.WithOutput("""{"platform":[null,{}, {"name":"php","version":8}, {"name":"ext-json","version":" "}, {"name":"php","version":"8.4.27"}]}""");
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", "{}").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("php 8.4.27 - PhpSdk");
        this.VerifyWarning("invalid package identity", Times.Exactly(4));
    }

    [TestMethod]
    public async Task MissingComposer_WarnsForEachNonVendorProjectAsync()
    {
        this.commands.Setup(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .ThrowsAsync(new Win32Exception(2));
        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("composer.json", """{"require":{"vendor/a":"1.0"}}""")
            .WithFile("composer.json", """{"require":{"vendor/b":"2.0"}}""", fileLocation: Path.Combine(Path.GetTempPath(), "child", "composer.json"))
            .ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().BeEquivalentTo(["vendor/a 1.0 - Composer", "vendor/b 2.0 - Composer"]);
        this.VerifyWarning("Unable to run Composer platform inventory", Times.Exactly(2));
        this.commands.Verify(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task NonzeroExit_WarnsWithoutReportingPartialOutputAsync()
    {
        this.WithOutput("""{"platform":[{"name":"php","version":"8.4.27"}]}""", exitCode: 1);
        var (_, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"1.0"}}""").ExecuteDetectorAsync();

        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("vendor/app 1.0 - Composer");
        this.VerifyWarning("exit code 1", Times.Once());
    }

    [TestMethod]
    public async Task HungCommand_TimesOutWithoutFailingPackageDetectionAsync()
    {
        this.commands.Setup(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .Returns(async (string _, IEnumerable<string> _, DirectoryInfo _, CancellationToken token, string[] _) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new CommandLineExecutionResult();
            });
        var stopwatch = Stopwatch.StartNew();
        var (result, recorder) = await this.DetectorTestUtility.WithFile("composer.json", """{"require":{"vendor/app":"1.0"}}""").ExecuteDetectorAsync();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(12));
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Select(component => component.Component.Id).Should().Equal("vendor/app 1.0 - Composer");
        this.VerifyWarning("10-second limit", Times.Once());
    }

    [TestMethod]
    public async Task CallerCancellation_IsNotSwallowedAsAnInventoryTimeoutAsync()
    {
        using var cancellation = new CancellationTokenSource();
        this.commands.Setup(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .Returns(async (string _, IEnumerable<string> _, DirectoryInfo _, CancellationToken token, string[] _) =>
            {
                await cancellation.CancelAsync();
                return await Task.FromCanceled<CommandLineExecutionResult>(token);
            });
        Func<Task> action = async () => await this.DetectorTestUtility.WithFile("composer.json", "{}").ExecuteDetectorAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        this.VerifyWarning("10-second limit", Times.Never());
    }

    private void WithOutput(string output, int exitCode = 0) =>
        this.commands.Setup(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult { StdOut = output, StdErr = "test error", ExitCode = exitCode });

    private void VerifyWarning(string message, Times times) =>
        this.logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);
}
