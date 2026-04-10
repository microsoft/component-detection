namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BinarySyftRunnerTests
{
    private readonly Mock<ICommandLineInvocationService> mockCommandLineService;
    private readonly Mock<ILogger<BinarySyftRunner>> mockLogger;

    public BinarySyftRunnerTests()
    {
        this.mockCommandLineService = new Mock<ICommandLineInvocationService>();
        this.mockLogger = new Mock<ILogger<BinarySyftRunner>>();
    }

    [TestMethod]
    public async Task CanRunAsync_ReturnsTrueWhenBinaryExistsAndVersionCheckSucceeds()
    {
        this.mockCommandLineService.Setup(service =>
                service.CanCommandBeLocatedAsync(
                    "/usr/local/bin/syft",
                    null,
                    "--version"))
            .ReturnsAsync(true);

        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    "/usr/local/bin/syft",
                    null,
                    null,
                    It.IsAny<CancellationToken>(),
                    "--version"))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                StdOut = "syft 1.37.0",
                StdErr = string.Empty,
                ExitCode = 0,
            });

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var result = await runner.CanRunAsync();

        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task CanRunAsync_ReturnsFalseWhenBinaryNotFound()
    {
        this.mockCommandLineService.Setup(service =>
                service.CanCommandBeLocatedAsync(
                    "/nonexistent/syft",
                    null,
                    "--version"))
            .ReturnsAsync(false);

        var runner = new BinarySyftRunner(
            "/nonexistent/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var result = await runner.CanRunAsync();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task CanRunAsync_ReturnsFalseWhenVersionCheckFails()
    {
        this.mockCommandLineService.Setup(service =>
                service.CanCommandBeLocatedAsync(
                    "/usr/local/bin/syft",
                    null,
                    "--version"))
            .ReturnsAsync(true);

        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    "/usr/local/bin/syft",
                    null,
                    null,
                    It.IsAny<CancellationToken>(),
                    "--version"))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                StdOut = string.Empty,
                StdErr = "not a valid syft binary",
                ExitCode = 1,
            });

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var result = await runner.CanRunAsync();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task RunSyftAsync_ConstructsCorrectCommandLine()
    {
        var expectedOutput = """{"artifacts":[],"distro":{"id":"ubuntu","versionID":"22.04"}}""";
        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<System.IO.DirectoryInfo>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                StdOut = expectedOutput,
                StdErr = string.Empty,
                ExitCode = 0,
            });

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var (stdout, stderr) = await runner.RunSyftAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            ["--quiet", "--output", "json", "--scope", "all-layers"]);

        stdout.Should().Be(expectedOutput);
        stderr.Should().BeEmpty();

        this.mockCommandLineService.Verify(
            service => service.ExecuteCommandAsync(
                "/usr/local/bin/syft",
                null,
                null,
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(args =>
                    args.Length == 6
                    && args[0] == "fake_hash"
                    && args[1] == "--quiet"
                    && args[2] == "--output"
                    && args[3] == "json"
                    && args[4] == "--scope"
                    && args[5] == "all-layers")),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_NonZeroExitCode_LogsErrorAndReturnsOutput()
    {
        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<System.IO.DirectoryInfo>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                StdOut = string.Empty,
                StdErr = "error: image not found",
                ExitCode = 1,
            });

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var (stdout, stderr) = await runner.RunSyftAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            ["--quiet", "--output", "json"]);

        stdout.Should().BeEmpty();
        stderr.Should().Be("error: image not found");

        this.mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<System.Exception>(),
                (System.Func<It.IsAnyType, System.Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_CancellationToken_PropagatedToCommand()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<System.IO.DirectoryInfo>(),
                    cts.Token,
                    It.IsAny<string[]>()))
            .ThrowsAsync(new System.OperationCanceledException());

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        System.Func<Task> action = async () => await runner.RunSyftAsync(
            new ImageReference { OriginalInput = "fake_hash", Reference = "fake_hash", Kind = ImageReferenceKind.DockerImage },
            ["--quiet", "--output", "json"],
            cts.Token);

        await action.Should().ThrowAsync<System.OperationCanceledException>();
    }

    [TestMethod]
    [DataRow(ImageReferenceKind.OciLayout, "/path/to/oci", "oci-dir:/path/to/oci")]
    [DataRow(ImageReferenceKind.OciArchive, "/path/to/image.tar", "oci-archive:/path/to/image.tar")]
    [DataRow(ImageReferenceKind.DockerArchive, "/path/to/save.tar", "docker-archive:/path/to/save.tar")]
    [DataRow(ImageReferenceKind.DockerImage, "ubuntu:22.04", "ubuntu:22.04")]
    public async Task RunSyftAsync_ConstructsCorrectSourceForImageKind(
        ImageReferenceKind kind,
        string reference,
        string expectedSource)
    {
        this.mockCommandLineService.Setup(service =>
                service.ExecuteCommandAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<System.IO.DirectoryInfo>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string[]>()))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                StdOut = "{}",
                StdErr = string.Empty,
                ExitCode = 0,
            });

        var runner = new BinarySyftRunner(
            "/usr/local/bin/syft",
            this.mockCommandLineService.Object,
            this.mockLogger.Object);

        var imageRef = new ImageReference
        {
            OriginalInput = reference,
            Reference = reference,
            Kind = kind,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        this.mockCommandLineService.Verify(
            service => service.ExecuteCommandAsync(
                "/usr/local/bin/syft",
                null,
                null,
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(args => args[0] == expectedSource)),
            Times.Once);
    }
}
