#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ExperimentServiceTests
{
    private readonly Mock<IExperimentConfiguration> experimentConfigMock;
    private readonly Mock<IExperimentProcessor> experimentProcessorMock;
    private readonly Mock<IGraphTranslationService> graphTranslationServiceMock;
    private readonly Mock<ILogger<ExperimentService>> loggerMock;
    private readonly Mock<IComponentDetector> detectorMock;
    private readonly Mock<ScanSettings> scanSettingsMock;
    private readonly ComponentRecorder componentRecorder;

    public ExperimentServiceTests()
    {
        this.experimentConfigMock = new Mock<IExperimentConfiguration>();
        this.experimentProcessorMock = new Mock<IExperimentProcessor>();
        this.graphTranslationServiceMock = new Mock<IGraphTranslationService>();
        this.loggerMock = new Mock<ILogger<ExperimentService>>();
        this.detectorMock = new Mock<IComponentDetector>();
        this.scanSettingsMock = new Mock<ScanSettings>();

        this.componentRecorder = new ComponentRecorder();

        this.detectorMock.SetupGet(x => x.Id).Returns(Guid.NewGuid().ToString());
        this.experimentConfigMock.Setup(x => x.IsInControlGroup(this.detectorMock.Object)).Returns(true);
        this.experimentConfigMock.Setup(x => x.IsInExperimentGroup(this.detectorMock.Object)).Returns(true);
        this.experimentConfigMock.Setup(x => x.ShouldRecord(this.detectorMock.Object, It.IsAny<int>())).Returns(true);
    }

    private void SetupGraphMock(IEnumerable<ScannedComponent> components)
    {
        this.graphTranslationServiceMock
            .Setup(x => x.GenerateScanResultFromProcessingResult(It.IsAny<DetectorProcessingResult>(), It.IsAny<ScanSettings>(), It.IsAny<bool>()))
            .Returns(new ScanResult() { ComponentsFound = components });
    }

    [TestInitialize]
    public void TestInitialize()
    {
        DetectorExperiments.Enable = true;
        DetectorExperiments.AutomaticallyProcessExperiments = true;
    }

    [TestMethod]
    public void RecordDetectorRun_AddsComponentsToControlAndExperimentGroup()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        this.SetupGraphMock(components);

        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        this.experimentConfigMock.Verify(x => x.IsInControlGroup(this.detectorMock.Object), Times.Once());
        this.experimentConfigMock.Verify(x => x.IsInExperimentGroup(this.detectorMock.Object), Times.Once());

        // verify that we always call the graph translation service with updateLocations = false so we dont
        // corrupt file location paths
        this.graphTranslationServiceMock.Verify(
            x => x.GenerateScanResultFromProcessingResult(It.IsAny<DetectorProcessingResult>(), It.IsAny<ScanSettings>(), It.Is<bool>(x => !x)),
            Times.Once());
    }

    [TestMethod]
    public async Task RecordDetectorRun_FiltersExperimentsAsync()
    {
        var filterConfigMock = new Mock<IExperimentConfiguration>();
        filterConfigMock
            .Setup(x => x.ShouldRecord(It.IsAny<IComponentDetector>(), It.IsAny<int>()))
            .Returns(false);
        filterConfigMock.Setup(x => x.IsInControlGroup(this.detectorMock.Object)).Returns(true);
        filterConfigMock.Setup(x => x.IsInExperimentGroup(this.detectorMock.Object)).Returns(true);

        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object, filterConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);

        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);
        await service.FinishAsync();

        filterConfigMock.Verify(x => x.ShouldRecord(this.detectorMock.Object, components.Count), Times.Once());
        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(filterConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Once());
    }

    [TestMethod]
    public async Task RecordDetectorRun_Respects_DetectorExperiments_EnableAsync()
    {
        DetectorExperiments.Enable = false;
        var filterConfigMock = new Mock<IExperimentConfiguration>();

        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            [this.experimentConfigMock.Object, filterConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);

        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);
        await service.FinishAsync();

        filterConfigMock.Verify(x => x.ShouldRecord(this.detectorMock.Object, components.Count), Times.Never());
        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(filterConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
    }

    [TestMethod]
    public async Task FinishAsync_ProcessesExperimentsAsync()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Once());
    }

    [TestMethod]
    public async Task FinishAsync_SwallowsExceptionsAsync()
    {
        this.experimentProcessorMock
            .Setup(x =>
                x.ProcessExperimentAsync(It.IsAny<IExperimentConfiguration>(), It.IsAny<ExperimentDiff>()))
            .ThrowsAsync(new IOException("test exception"));

        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        var act = async () => await service.FinishAsync();
        await act.Should().NotThrowAsync<IOException>();
    }

    [TestMethod]
    public async Task FinishAsync_SkipsEmptyExperimentsAsync()
    {
        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(It.IsAny<IExperimentConfiguration>(), It.IsAny<ExperimentDiff>()),
            Times.Never());
    }

    [TestMethod]
    public async Task FinishAsync_AutomaticallyProcessesExperimentsAsync()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Once());
    }

    [TestMethod]
    public async Task FinishAsync_DoesNotAutomaticallyProcessExperimentsAsync()
    {
        DetectorExperiments.AutomaticallyProcessExperiments = false;

        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
    }

    [TestMethod]
    public async Task FinishAsync_Respects_DetectorExperiments_EnableAsync()
    {
        DetectorExperiments.Enable = false;

        var components = ExperimentTestUtils.CreateRandomComponents();
        this.SetupGraphMock(components);

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
    }

    [TestMethod]
    public async Task RecordDetectorRun_CheckUnwantedDetectors_RemoveExperimentAsync()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        this.SetupGraphMock(components);

        var detectorList = new List<IComponentDetector>
        {
            new NuGetComponentDetector(
                new Mock<IComponentStreamEnumerableFactory>().Object,
                new Mock<IObservableDirectoryWalkerFactory>().Object,
                new Mock<ILogger<NuGetComponentDetector>>().Object), this.detectorMock.Object,
        };

        service.RemoveUnwantedExperimentsbyDetectors(detectorList);

        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Never());
    }

    [TestMethod]
    public async Task RecordDetectorRun_CheckUnwantedDetectors_KeepExperimentAsync()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);
        this.SetupGraphMock(components);

        var detectorList = new List<IComponentDetector>
        {
            new NuGetComponentDetector(
                new Mock<IComponentStreamEnumerableFactory>().Object,
                new Mock<IObservableDirectoryWalkerFactory>().Object,
                new Mock<ILogger<NuGetComponentDetector>>().Object),
        };

        service.RemoveUnwantedExperimentsbyDetectors(detectorList);

        service.RecordDetectorRun(this.detectorMock.Object, this.componentRecorder, this.scanSettingsMock.Object);

        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(this.experimentConfigMock.Object, It.IsAny<ExperimentDiff>()),
            Times.Once());
    }

    [TestMethod]
    public async Task InitializeAsync_InitsConfigsAsync()
    {
        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);

        await service.InitializeAsync();

        this.experimentConfigMock.Verify(x => x.InitAsync(), Times.Once());
    }

    [TestMethod]
    public async Task InitializeAsync_SwallowsExceptionsAsync()
    {
        this.experimentConfigMock.Setup(x => x.InitAsync()).ThrowsAsync(new InvalidOperationException());

        var service = new ExperimentService(
            [this.experimentConfigMock.Object],
            [this.experimentProcessorMock.Object],
            this.graphTranslationServiceMock.Object,
            this.loggerMock.Object);

        var action = async () => await service.InitializeAsync();

        await action.Should().NotThrowAsync();
        this.experimentConfigMock.Verify(x => x.InitAsync(), Times.Once());
    }
}
