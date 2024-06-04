namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
    private readonly Mock<ILogger<PipReportComponentDetector>> mockLogger;

    private readonly PipInstallationReport singlePackageReport;
    private readonly PipInstallationReport singlePackageReportBadVersion;
    private readonly PipInstallationReport multiPackageReport;
    private readonly PipInstallationReport jupyterPackageReport;

    public PipReportComponentDetectorTests()
    {
        this.pipCommandService = new Mock<IPipCommandService>();
        this.DetectorTestUtility.AddServiceMock(this.pipCommandService);

        this.mockLogger = new Mock<ILogger<PipReportComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);

        this.pipCommandService.Setup(x => x.PipExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>()))
            .ReturnsAsync(new Version(23, 0, 0));

        this.singlePackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_single_pkg);
        this.singlePackageReportBadVersion = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_single_pkg_bad_version);
        this.multiPackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_multi_pkg);
        this.jupyterPackageReport = JsonConvert.DeserializeObject<PipInstallationReport>(TestResources.pip_report_jupyterlab);
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

        this.pipCommandService.Setup(x => x.PipExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("setup.py", string.Empty)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.mockLogger.VerifyAll();
    }

    [TestMethod]
    public async Task TestPipReportDetector_PipBadVersion_Null_Async()
    {
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>()))
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
        this.pipCommandService.Setup(x => x.GetPipVersionAsync(It.IsAny<string>()))
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
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>()))
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

        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>()))
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
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>()))
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
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>()))
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
    public async Task TestPipReportDetector_MultiComponentAsync()
    {
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(It.IsAny<string>(), It.IsAny<string>()))
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
            It.IsAny<string>()))
            .ReturnsAsync((this.multiPackageReport, null));
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>()))
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
            It.IsAny<string>()))
            .ReturnsAsync((this.multiPackageReport, null));
        this.pipCommandService.Setup(x => x.GenerateInstallationReportAsync(
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>()))
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
            It.IsAny<string>()))
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
        jupyterLabDependencies.Should().HaveCount(15);
        jupyterLabDependencies.Should().Contain("async-lru 2.0.4 - pip");
        jupyterLabDependencies.Should().Contain("jupyter-server 2.14.0 - pip");
        jupyterLabDependencies.Should().Contain("traitlets 5.14.3 - pip");
        jupyterLabDependencies.Should().Contain("requests 2.32.2 - pip");
        jupyterLabDependencies.Should().Contain("jupyter-lsp 2.2.5 - pip");

        var bleachComponent = pipComponents.Single(x => ((PipComponent)x.Component).Name.Equals("bleach")).Component as PipComponent;
        bleachComponent.Version.Should().Be("6.1.0");
        bleachComponent.Author.Should().Be("Will Kahn-Greene");
        bleachComponent.License.Should().Be("Apache Software License");

        var bleachDependencies = jupyterGraph.GetDependenciesForComponent(bleachComponent.Id);
        bleachDependencies.Should().HaveCount(3);
        bleachDependencies.Should().Contain("six 1.16.0 - pip");
        bleachDependencies.Should().Contain("webencodings 0.5.1 - pip");
        bleachDependencies.Should().Contain("tinycss2 1.3.0 - pip");

        ComponentRecorderTestUtilities.CheckChild<PipComponent>(
            componentRecorder,
            "async-lru 2.0.4 - pip",
            new[] { "jupyterlab 4.2.0 - pip" });

        ComponentRecorderTestUtilities.CheckChild<PipComponent>(
            componentRecorder,
            "tinycss2 1.3.0 - pip",
            new[] { "jupyterlab 4.2.0 - pip" });
    }
}
