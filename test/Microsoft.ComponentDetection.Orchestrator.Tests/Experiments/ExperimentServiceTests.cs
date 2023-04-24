namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
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
    private readonly Mock<ILogger<ExperimentService>> loggerMock;
    private readonly Mock<IComponentDetector> detectorMock;

    public ExperimentServiceTests()
    {
        this.experimentConfigMock = new Mock<IExperimentConfiguration>();
        this.experimentProcessorMock = new Mock<IExperimentProcessor>();
        this.loggerMock = new Mock<ILogger<ExperimentService>>();
        this.detectorMock = new Mock<IComponentDetector>();

        this.experimentConfigMock.Setup(x => x.IsInControlGroup(this.detectorMock.Object)).Returns(true);
        this.experimentConfigMock.Setup(x => x.IsInExperimentGroup(this.detectorMock.Object)).Returns(true);
        this.experimentConfigMock.Setup(x => x.ShouldRecord(this.detectorMock.Object, It.IsAny<int>())).Returns(true);
    }

    [TestMethod]
    public void RecordDetectorRun_AddsComponentsToControlAndExperimentGroup()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            new[] { this.experimentConfigMock.Object },
            Enumerable.Empty<IExperimentProcessor>(),
            this.loggerMock.Object);

        service.RecordDetectorRun(this.detectorMock.Object, components);

        this.experimentConfigMock.Verify(x => x.IsInControlGroup(this.detectorMock.Object), Times.Once());
        this.experimentConfigMock.Verify(x => x.IsInExperimentGroup(this.detectorMock.Object), Times.Once());
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

        var service = new ExperimentService(
            new[] { this.experimentConfigMock.Object, filterConfigMock.Object },
            new[] { this.experimentProcessorMock.Object },
            this.loggerMock.Object);

        service.RecordDetectorRun(this.detectorMock.Object, components);
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
    public async Task FinishAsync_ProcessesExperimentsAsync()
    {
        var components = ExperimentTestUtils.CreateRandomComponents();

        var service = new ExperimentService(
            new[] { this.experimentConfigMock.Object },
            new[] { this.experimentProcessorMock.Object },
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, components);

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

        var service = new ExperimentService(
            new[] { this.experimentConfigMock.Object },
            new[] { this.experimentProcessorMock.Object },
            this.loggerMock.Object);
        service.RecordDetectorRun(this.detectorMock.Object, components);

        var act = async () => await service.FinishAsync();
        await act.Should().NotThrowAsync<IOException>();
    }

    [TestMethod]
    public async Task FinishAsync_SkipsEmptyExperimentsAsync()
    {
        var service = new ExperimentService(
            new[] { this.experimentConfigMock.Object },
            new[] { this.experimentProcessorMock.Object },
            this.loggerMock.Object);
        await service.FinishAsync();

        this.experimentProcessorMock.Verify(
            x => x.ProcessExperimentAsync(It.IsAny<IExperimentConfiguration>(), It.IsAny<ExperimentDiff>()),
            Times.Never());
    }
}
