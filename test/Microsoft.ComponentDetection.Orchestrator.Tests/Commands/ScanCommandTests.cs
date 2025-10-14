#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ScanCommandTests
{
    private Mock<IFileWritingService> fileWritingServiceMock;
    private Mock<IScanExecutionService> scanExecutionServiceMock;
    private Mock<ILogger<ScanCommand>> loggerMock;
    private ScanCommand command;

    [TestInitialize]
    public void TestInitialize()
    {
        this.fileWritingServiceMock = new Mock<IFileWritingService>();
        this.scanExecutionServiceMock = new Mock<IScanExecutionService>();
        this.loggerMock = new Mock<ILogger<ScanCommand>>();

        this.command = new ScanCommand(
            this.fileWritingServiceMock.Object,
            this.scanExecutionServiceMock.Object,
            this.loggerMock.Object);
    }

    [TestMethod]
    public async Task ScanCommand_ExecutesScanAndWritesManifestAsync()
    {
        var settings = new ScanSettings { Output = "output" };

        var result = await this.command.ExecuteAsync(null, settings);

        this.fileWritingServiceMock.Verify(x => x.Init(settings.Output), Times.Once);
        this.scanExecutionServiceMock.Verify(x => x.ExecuteScanAsync(settings), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.ResolveFilePath(It.IsAny<string>()), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.AppendToFile(It.IsAny<string>(), It.IsAny<ScanResult>()));
        result.Should().Be(0);
    }

    [TestMethod]
    public async Task ScanCommand_ExecutesScanAndWritesUserManifestAsync()
    {
        var settings = new ScanSettings { Output = "output", ManifestFile = new FileInfo("manifest.json") };

        var result = await this.command.ExecuteAsync(null, settings);

        this.fileWritingServiceMock.Verify(x => x.Init(settings.Output), Times.Once);
        this.scanExecutionServiceMock.Verify(x => x.ExecuteScanAsync(settings), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.WriteFile(It.Is<FileInfo>(x => x == settings.ManifestFile), It.IsAny<ScanResult>()));

        result.Should().Be(0);
    }

    [TestMethod]
    public async Task ScanCommand_ExecutesScanAndPrintsManifestAsync()
    {
        var settings = new ScanSettings { Output = "output", PrintManifest = true };
        using var ms = new MemoryStream();
        await using var tw = new StreamWriter(ms);
        Console.SetOut(tw);

        var result = await this.command.ExecuteAsync(null, settings);

        this.fileWritingServiceMock.Verify(x => x.Init(settings.Output), Times.Once);
        this.scanExecutionServiceMock.Verify(x => x.ExecuteScanAsync(settings), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.ResolveFilePath(It.IsAny<string>()), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.AppendToFile(It.IsAny<string>(), It.IsAny<ScanResult>()));

        result.Should().Be(0);
        await tw.FlushAsync();
        ms.Position.Should().BePositive();
    }

    [TestMethod]
    public async Task ExecuteScanCommandAsync_PrintsManifestAsync()
    {
        var settings = new ScanSettings { Output = "output", PrintManifest = true };
        using var ms = new MemoryStream();
        await using var tw = new StreamWriter(ms);
        Console.SetOut(tw);

        var result = await this.command.ExecuteScanCommandAsync(settings);

        this.fileWritingServiceMock.Verify(x => x.Init(settings.Output), Times.Once);
        this.scanExecutionServiceMock.Verify(x => x.ExecuteScanAsync(settings), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.ResolveFilePath(It.IsAny<string>()), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.AppendToFile(It.IsAny<string>(), It.IsAny<ScanResult>()));

        await tw.FlushAsync();
        ms.Position.Should().BePositive();
    }

    [TestMethod]
    public async Task ExecuteScanCommandAsync_WritesUserManifestAsync()
    {
        var settings = new ScanSettings { Output = "output", ManifestFile = new FileInfo("manifest.json") };

        var result = await this.command.ExecuteScanCommandAsync(settings);

        this.fileWritingServiceMock.Verify(x => x.Init(settings.Output), Times.Once);
        this.scanExecutionServiceMock.Verify(x => x.ExecuteScanAsync(settings), Times.Once);
        this.fileWritingServiceMock.Verify(x => x.WriteFile(It.Is<FileInfo>(x => x == settings.ManifestFile), It.IsAny<ScanResult>()));
    }
}
