namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class Go117ComponentDetectorTests : BaseDetectorTest<Go117ComponentDetector>
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;
    private readonly Mock<IEnvironmentVariableService> envVarService;
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;
    private readonly Mock<ILogger<GoComponentDetector>> mockLogger;
    private readonly Mock<IGoParserFactory> mockParserFactory;

    public Go117ComponentDetectorTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(false);
        this.DetectorTestUtility.AddServiceMock(this.commandLineMock);

        var mockGoParser = new Mock<IGoParser>();
        this.mockParserFactory = new Mock<IGoParserFactory>();

        this.mockParserFactory.Setup(x => x.CreateParser(It.IsAny<GoParserType>(), It.IsAny<ILogger>())).Returns(mockGoParser.Object);
        this.envVarService = new Mock<IEnvironmentVariableService>();
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.DetectorTestUtility.AddServiceMock(this.envVarService);
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
        this.mockLogger = new Mock<ILogger<GoComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);
        this.DetectorTestUtility.AddServiceMock(this.mockParserFactory);
    }

    [TestMethod]
    public async Task Go117ModDetector_GoCliIs1_11OrGreater_GoGraphIsExecutedAsync()
    {
        var goMod = string.Empty;

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.23.6 windows/amd64",
        });

        this.commandLineMock.Setup(service => service.ExecuteCommandAsync(
            "go",
            null,
            It.IsAny<DirectoryInfo>(),
            default,
            It.Is<string[]>(args => args.Length == 2 && args[0] == "mod" && args[1] == "graph")))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = string.Empty,
        })
        .Verifiable();

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.commandLineMock.Verify();
    }

    [TestMethod]
    public async Task Go117ModDetector_GoCliIs111OrLessThan1_11_GoGraphIsNotExecutedAsync()
    {
        var goMod = string.Empty;

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.10.6 windows/amd64",
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.commandLineMock.Verify(
            service => service.ExecuteCommandAsync(
                "go",
                null,
                It.IsAny<DirectoryInfo>(),
                default,
                It.Is<string[]>(args => args.Length == 2 && args[0] == "mod" && args[1] == "graph")),
            times: Times.Never);
    }

    [TestMethod]
    public async Task Go117ModDetector_GoModFileFound_GoModParserIsExecuted()
    {
        var goModParserMock = new Mock<IGoParser>();
        this.mockParserFactory.Setup(x => x.CreateParser(GoParserType.GoMod, It.IsAny<ILogger>())).Returns(goModParserMock.Object);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.10.6 windows/amd64",
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        goModParserMock.Verify(parser => parser.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()), Times.Once);
    }

    [TestMethod]
    public async Task Go117ModDetector_GoSumFileFound_GoSumParserIsExecuted()
    {
        var goSumParserMock = new Mock<IGoParser>();
        this.mockParserFactory.Setup(x => x.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>())).Returns(goSumParserMock.Object);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.10.6 windows/amd64",
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        goSumParserMock.Verify(parser => parser.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()), Times.Once);
    }
}
