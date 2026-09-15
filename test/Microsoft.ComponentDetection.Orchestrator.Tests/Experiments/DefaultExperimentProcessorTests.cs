#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DefaultExperimentProcessorTests
{
    private static readonly JsonSerializerOptions TestJsonOptions = new JsonSerializerOptions { WriteIndented = true };

    private readonly Mock<IFileWritingService> fileWritingServiceMock;
    private readonly DefaultExperimentProcessor processor;

    public DefaultExperimentProcessorTests()
    {
        var loggerMock = new Mock<ILogger<DefaultExperimentProcessor>>();
        this.fileWritingServiceMock = new Mock<IFileWritingService>();
        this.processor = new DefaultExperimentProcessor(this.fileWritingServiceMock.Object, loggerMock.Object);
    }

    [TestMethod]
    public async Task ProcessExperimentAsync_WritesSerializedExperimentDiffToFileAsync()
    {
        var config = new Mock<IExperimentConfiguration>();
        config.Setup(c => c.Name).Returns("TestExperiment");

        var diff = new ExperimentDiff(
            ExperimentTestUtils.CreateRandomExperimentComponents(),
            ExperimentTestUtils.CreateRandomExperimentComponents());

        var serializedDiff = JsonSerializer.Serialize(diff, TestJsonOptions);

        await this.processor.ProcessExperimentAsync(config.Object, diff);

        this.fileWritingServiceMock.Verify(
            f => f.WriteFileAsync(
                It.Is<string>(s => s.StartsWith($"Experiment_{config.Object.Name}_")),
                serializedDiff,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
