#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Tests.Mocks;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

[TestClass]
public class PipReportComponentDetectorTests : BaseDetectorTest<PipReportComponentDetector>
{
    private readonly Mock<IPipCommandService> pipCommandService;
    private readonly Mock<IPythonCommandService> pythonCommandService;
    private readonly Mock<IPythonResolver> pythonResolver;
    private readonly Mock<IEnvironmentVariableService> mockEnvVarService;
    private readonly Mock<ILogger<PipReportComponentDetector>> mockLogger;

    private readonly IFileUtilityService fileUtilityService;

    private readonly PipInstallationReport singlePackageReport;
    private readonly PipInstallationReport singlePackageReportBadVersion;
    private readonly PipInstallationReport singlePackageReportInvalidPkgVersion;
    private readonly PipInstallationReport multiPackageReport;
    private readonly PipInstallationReport jupyterPackageReport;
    private readonly PipInstallationReport simpleExtrasReport;

    public PipReportComponentDetectorTests()
    {
        this.pipCommandService = new Mock<IPipCommandService>();
        this.DetectorTestUtility.AddServiceMock(this.pipCommandService);

        this.pythonCommandService = new Mock<IPythonCommandService>();
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        this.DetectorTestUtility.AddServiceMock(this.pythonCommandService);

        this.pythonResolver = new Mock<IPythonResolver>();
        this.pythonResolver.Setup(x => x.GetPythonEnvironmentVariables()).Returns([]);
        this.DetectorTestUtility.AddServiceMock(this.pythonResolver);

        this.mockLogger = new Mock<ILogger<PipReportComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);

        this.mockEnvVarService = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.mockEnvVarService);

        this.fileUtilityService = new FileUtilityService();
        this.DetectorTestUtility.AddService(this.fileUtilityService);

        this.pipCommandService.Setup(x => x.PipExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Version(23, 0, 0));

        this.singlePackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_single_pkg);
        this.singlePackageReportBadVersion = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_single_pkg_bad_version);
        this.singlePackageReportInvalidPkgVersion = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_single_pkg_invalid_pkg_version);
        this.multiPackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_multi_pkg);
        this.jupyterPackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_jupyterlab);
        this.simpleExtrasReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_simple_extras);
    }

    [TestMethod]
    public async Task TestPipReportDetector_PipNotInstalledAsync()
    {
        this.mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        this.DetectorTestUtility.AddServiceMock(this.mockLogger);

        this.pipCommandService.Setup(x => x.PipExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.mockLogger.VerifyAll();
    }

    [TestMethod]
    public async Task TestPipReportDetector_PipBadVersion_Null_Async()
    {
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Version)null);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: No valid pip version")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_PipBadVersion_Low_Async()
    {
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Version(22, 1, 0));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: No valid pip version")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_PipInstalledNoFilesAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task TestPipReportDetector_BadReportVersionAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReportBadVersion, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: The pip installation report version")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_BadReportParseVersionAsync()
    {
        this.singlePackageReportBadVersion.Version = "2.5";

        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReportBadVersion, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: The pip installation report version")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_CatchesExceptionAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCastException());

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Failure while parsing pip")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_SingleComponentAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Generating pip installation report")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var sixComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("six")).Component as PipComponent;
        sixComponent.Version.Should().Be("1.16.0");
        sixComponent.Author.Should().Be("Benjamin Peterson");
        sixComponent.License.Should().Be("MIT");
    }

    [TestMethod]
    public async Task TestPipReportDetector_SimpleExtrasAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.simpleExtrasReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Generating pip installation report")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();
        var requestsComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("requests")).Component as PipComponent;
        requestsComponent.Version.Should().Be("2.32.3");
    }

    [TestMethod]
    public async Task TestPipReportDetector_BadPackageVersionAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReportInvalidPkgVersion, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("with non-canonical version")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPipReportDetector_MultiComponentAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.multiPackageReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var sixComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("six")).Component as PipComponent;
        sixComponent.Version.Should().Be("1.16.0");
        sixComponent.Author.Should().Be("Benjamin Peterson");
        sixComponent.License.Should().Be("MIT");

        var dateComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("python-dateutil")).Component as PipComponent;
        dateComponent.Version.Should().Be("2.9.0.post0");
        dateComponent.Author.Should().Be("Paul Ganssle");
        dateComponent.License.Should().Be("Dual License");
    }

    [TestMethod]
    public async Task TestPipReportDetector_MultiComponent_Dedupe_Async()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("setup.py", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.multiPackageReport, null));
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var sixComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("six")).Component as PipComponent;
        sixComponent.Version.Should().Be("1.16.0");
        sixComponent.Author.Should().Be("Benjamin Peterson");
        sixComponent.License.Should().Be("MIT");

        var dateComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("python-dateutil")).Component as PipComponent;
        dateComponent.Version.Should().Be("2.9.0.post0");
        dateComponent.Author.Should().Be("Paul Ganssle");
        dateComponent.License.Should().Be("Dual License");
    }

    [TestMethod]
    public async Task TestPipReportDetector_MultiComponent_ComponentRecorder_Async()
    {
        const string file1 = "c:\\repo\\setup.py";
        const string file2 = "c:\\repo\\lib\\requirements.txt";

        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("setup.py", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.multiPackageReport, null));
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.singlePackageReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty, fileLocation: file1)
            .WithFile("requirements.txt", string.Empty, fileLocation: file2)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var sixComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("six")).Component as PipComponent;
        sixComponent.Version.Should().Be("1.16.0");
        sixComponent.Author.Should().Be("Benjamin Peterson");
        sixComponent.License.Should().Be("MIT");

        var dateComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("python-dateutil")).Component as PipComponent;
        dateComponent.Version.Should().Be("2.9.0.post0");
        dateComponent.Author.Should().Be("Paul Ganssle");
        dateComponent.License.Should().Be("Dual License");

        componentRecorder.AssertAllExplicitlyReferencedComponents<PipComponent>(
            "six 1.16.0 - pip",
            x => x.Id.Equals("six 1.16.0 - pip", StringComparison.OrdinalIgnoreCase),
            x => x.Id.Equals("python-dateutil 2.9.0.post0 - pip", StringComparison.OrdinalIgnoreCase));

        var graphsByLocations = componentRecorder.GetDependencyGraphsByLocation();
        graphsByLocations.Should().HaveCount(2);

        var setupGraphComponentsWithDeps = new Dictionary<string, string[]>
        {
            { "six 1.16.0 - pip", Array.Empty<string>() },
            { "python-dateutil 2.9.0.post0 - pip", new[] { "six 1.16.0 - pip" } },
        };

        var reqGraphComponentsWithDeps = new Dictionary<string, string[]>
        {
            { "six 1.16.0 - pip", Array.Empty<string>() },
        };

        var setupGraph = graphsByLocations[file1];
        setupGraphComponentsWithDeps.Keys.All(setupGraph.IsComponentExplicitlyReferenced).Should().BeTrue();
        ComponentRecorderTestUtilities.CheckGraphStructure(setupGraph, setupGraphComponentsWithDeps);

        var reqGraph = graphsByLocations[file2];
        reqGraph.IsComponentExplicitlyReferenced(sixComponent.Id).Should().BeTrue();
        ComponentRecorderTestUtilities.CheckGraphStructure(reqGraph, reqGraphComponentsWithDeps);
    }

    [TestMethod]
    public async Task TestPipReportDetector_SingleRoot_ComplexGraph_ComponentRecorder_Async()
    {
        const string file1 = "c:\\repo\\lib\\requirements.txt";

        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.jupyterPackageReport, null));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty, fileLocation: file1)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(89);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var jupyterComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("jupyterlab")).Component as PipComponent;
        jupyterComponent.Version.Should().Be("4.2.0");
        jupyterComponent.Author.Should().Be("Jupyter Development Team <jupyter@googlegroups.com>");
        jupyterComponent.License.Should().Be("BSD License");

        componentRecorder.AssertAllExplicitlyReferencedComponents<PipComponent>(
            "jupyterlab 4.2.0 - pip",
            x => x.Id.Equals("jupyterlab 4.2.0 - pip", StringComparison.OrdinalIgnoreCase));

        // spot check some dependencies - there are too many to verify them all here
        var graphsByLocations = componentRecorder.GetDependencyGraphsByLocation();
        graphsByLocations.Should().ContainSingle();

        var jupyterGraph = graphsByLocations[file1];

        var jupyterLabDependencies = jupyterGraph.GetDependenciesForComponent(jupyterComponent.Id);
        jupyterLabDependencies.Should().HaveCount(12);
        jupyterLabDependencies.Should().Contain("async-lru 2.0.4 - pip");
        jupyterLabDependencies.Should().Contain("jupyter-server 2.14.0 - pip");
        jupyterLabDependencies.Should().Contain("traitlets 5.14.3 - pip");
        jupyterLabDependencies.Should().Contain("jupyter-lsp 2.2.5 - pip");

        var bleachComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("bleach")).Component as PipComponent;
        bleachComponent.Version.Should().Be("6.1.0");
        bleachComponent.Author.Should().Be("Will Kahn-Greene");
        bleachComponent.License.Should().Be("Apache Software License");

        var bleachDependencies = jupyterGraph.GetDependenciesForComponent(bleachComponent.Id);
        bleachDependencies.Should().HaveCount(2);
        bleachDependencies.Should().Contain("six 1.16.0 - pip");
        bleachDependencies.Should().Contain("webencodings 0.5.1 - pip");

        ComponentRecorderTestUtilities.CheckChild<PipComponent>(
            componentRecorder,
            "async-lru 2.0.4 - pip",
            ["jupyterlab 4.2.0 - pip"]);

        ComponentRecorderTestUtilities.CheckChild<PipComponent>(
            componentRecorder,
            "tinycss2 1.3.0 - pip",
            ["jupyterlab 4.2.0 - pip"]);
    }

    [TestMethod]
    public async Task TestPipReportDetector_OverrideSourceCodeScanAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        this.mockEnvVarService.Setup(x => x.DoesEnvironmentVariableExist("PipReportOverrideBehavior")).Returns(true);
        this.mockEnvVarService.Setup(x => x.GetEnvironmentVariable("PipReportOverrideBehavior")).Returns("sourcecodescan");

        var baseSetupPyDependencies = this.ToGitTuple(["a==1.0", "b>=2.0,!=2.1,<3.0.0", "c!=1.1"]);
        var baseRequirementsTextDependencies = this.ToGitTuple(["d~=1.0", "e<=2.0", "f===1.1", "g<3.0", "h>=1.0,<=3.0,!=2.0,!=4.0"]);
        baseRequirementsTextDependencies.Add((null, new GitComponent(new Uri("https://github.com/example/example"), "deadbee")));

        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "setup.py"), null)).ReturnsAsync(baseSetupPyDependencies);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(7);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "a").Component).Version.Should().Be("1.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "b").Component).Version.Should().Be("2.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "d").Component).Version.Should().Be("1.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "e").Component).Version.Should().Be("2.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "f").Component).Version.Should().Be("1.1");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "h").Component).Version.Should().Be("3.0");

        var gitComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Type == ComponentType.Git);
        gitComponents.Should().ContainSingle();
        var gitComponent = (GitComponent)gitComponents.Single().Component;

        gitComponent.RepositoryUrl.Should().Be("https://github.com/example/example");
        gitComponent.CommitHash.Should().Be("deadbee");

        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Found PipReportOverrideBehavior environment variable set to SourceCodeScan.")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task TestPipReportDetector_FallbackAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var baseSetupPyDependencies = this.ToGitTuple(["a==1.0", "b>=2.0,!=2.1,<3.0.0", "c!=1.1", "y==invalidversion"]);
        var baseRequirementsTextDependencies = this.ToGitTuple(["d~=1.0", "e<=2.0", "f===1.1", "g<3.0", "h>=1.0,<=3.0,!=2.0,!=4.0", "z==anotherinvalidversion"]);
        baseRequirementsTextDependencies.Add((null, new GitComponent(new Uri("https://github.com/example/example"), "deadbee")));

        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "setup.py"), null)).ReturnsAsync(baseSetupPyDependencies);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies);

        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Want to fallback, so fail initial report generation"));

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(7);

        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "a").Component).Version.Should().Be("1.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "b").Component).Version.Should().Be("2.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "d").Component).Version.Should().Be("1.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "e").Component).Version.Should().Be("2.0");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "f").Component).Version.Should().Be("1.1");
        ((PipComponent)pipComponents.Single(x => ((PipComponent)x.Component).Name == "h").Component).Version.Should().Be("3.0");
        pipComponents.Should().NotContain(x => ((PipComponent)x.Component).Name == "c");
        pipComponents.Should().NotContain(x => ((PipComponent)x.Component).Name == "g");
        pipComponents.Should().NotContain(x => ((PipComponent)x.Component).Name == "y");
        pipComponents.Should().NotContain(x => ((PipComponent)x.Component).Name == "z");

        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("Could not create pip dependency: invalidversion is not a valid python version")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);

        var gitComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Type == ComponentType.Git);
        gitComponents.Should().ContainSingle();
        var gitComponent = (GitComponent)gitComponents.Single().Component;

        gitComponent.RepositoryUrl.Should().Be("https://github.com/example/example");
        gitComponent.CommitHash.Should().Be("deadbee");
    }

    [TestMethod]
    public async Task TestPipReportDetector_OverrideSkipAsync()
    {
        this.pythonCommandService.Setup(x => x.PythonExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        this.mockEnvVarService.Setup(x => x.DoesEnvironmentVariableExist("PipReportOverrideBehavior")).Returns(true);
        this.mockEnvVarService.Setup(x => x.GetEnvironmentVariable("PipReportOverrideBehavior")).Returns("skip");

        var baseSetupPyDependencies = this.ToGitTuple(["a==1.0", "b>=2.0,!=2.1,<3.0.0", "c!=1.1"]);
        var baseRequirementsTextDependencies = this.ToGitTuple(["d~=1.0", "e<=2.0", "f===1.1", "g<3.0", "h>=1.0,<=3.0,!=2.0,!=4.0"]);
        baseRequirementsTextDependencies.Add((null, new GitComponent(new Uri("https://github.com/example/example"), "deadbee")));

        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "setup.py"), null)).ReturnsAsync(baseSetupPyDependencies);
        this.pythonCommandService.Setup(x => x.ParseFileAsync(Path.Join(Path.GetTempPath(), "requirements.txt"), null)).ReturnsAsync(baseRequirementsTextDependencies);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .WithFile("requirements.txt", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Found PipReportOverrideBehavior environment variable set to Skip.")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [TestMethod]
    public async Task TestPipReportDetector_SimplePregeneratedFile_Async()
    {
        this.pythonCommandService
            .Setup(x => x.ParseFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([("requests", null)]);

        var file1 = Path.Join(Directory.GetCurrentDirectory(), "Mocks", "requirements.txt");
        var pregeneratedFile = Path.Join(Directory.GetCurrentDirectory(), "Mocks", "test.component-detection-pip-report.json");

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty, fileLocation: file1)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Found pre-generated pip report")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var requestsComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("requests")).Component as PipComponent;
        requestsComponent.Version.Should().Be("2.32.3");

        var idnaComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("idna")).Component as PipComponent;
        idnaComponent.Version.Should().Be("3.7");
    }

    [TestMethod]
    public async Task TestPipReportDetector_InvalidPregeneratedFile_Async()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((this.simpleExtrasReport, null));

        this.pythonCommandService
            .Setup(x => x.ParseFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([("requests", null)]);

        var file1 = Path.Join(Directory.GetCurrentDirectory(), "Mocks", "Invalid", "requirements.txt");

        // this pre-generated file does not contains the 'requests' package, and so the report
        // validator should fail and the detector should continue as if no pre-generated file was found
        var pregeneratedFile = Path.Join(Directory.GetCurrentDirectory(), "Mocks", "Invalid", "invalid.component-detection-pip-report.json");

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("requirements.txt", string.Empty, fileLocation: file1)
            .ExecuteDetectorAsync();

        // found invalid pre-generated file
        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("is invalid")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        // fell back to generating the report itself
        this.mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("PipReport: Generating pip installation report")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

        this.pipCommandService.Verify(
            x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // verify results
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var pipComponents = detectedComponents.Where(detectedComponent => detectedComponent.Component.Id.Contains("pip")).ToList();

        var requestsComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("requests")).Component as PipComponent;
        requestsComponent.Version.Should().Be("2.32.3");

        var idnaComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("idna")).Component as PipComponent;
        idnaComponent.Version.Should().Be("3.7");
    }

    private List<(string PackageString, GitComponent Component)> ToGitTuple(IList<string> components)
    {
        return components.Select<string, (string, GitComponent)>(dep => (dep, null)).ToList();
    }
}
