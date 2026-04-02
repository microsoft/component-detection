#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MavenCommandServiceTests
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;
    private readonly Mock<IEnvironmentVariableService> environmentVarServiceMock;
    private readonly Mock<IMavenStyleDependencyGraphParserService> parserServiceMock;
    private readonly MavenCommandService mavenCommandService;

    public MavenCommandServiceTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.environmentVarServiceMock = new Mock<IEnvironmentVariableService>();
        var loggerMock = new Mock<ILogger<MavenCommandService>>();

        this.parserServiceMock = new Mock<IMavenStyleDependencyGraphParserService>();

        this.mavenCommandService = new MavenCommandService(
            this.commandLineMock.Object,
            this.parserServiceMock.Object,
            this.environmentVarServiceMock.Object,
            loggerMock.Object);
    }

    [TestMethod]
    public async Task MavenCLIExists_ExpectedArguments_ReturnTrueAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(
            MavenCommandService.PrimaryCommand,
            MavenCommandService.AdditionalValidCommands,
            MavenCommandService.MvnVersionArgument)).ReturnsAsync(true);

        var result = await this.mavenCommandService.MavenCLIExistsAsync();

        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task MavenCLIExists_ExpectedArguments_ReturnFalseAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(
            MavenCommandService.PrimaryCommand,
            MavenCommandService.AdditionalValidCommands,
            MavenCommandService.MvnVersionArgument)).ReturnsAsync(false);

        var result = await this.mavenCommandService.MavenCLIExistsAsync();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessAsync()
    {
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.Is<CancellationToken>(x => !x.IsCancellationRequested),
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
            })
            .Verifiable();

        await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);

        Mock.Verify(this.commandLineMock);
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_WhenCancellationRequested_ThrowsOperationCanceledExceptionAsync()
    {
        var cts = new CancellationTokenSource();
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        // Set up the CLI mock to throw OperationCanceledException when called with a cancelled token
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string[]>()))
            .Returns<string, string[], CancellationToken, string[]>((_, _, ct, _) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new CommandLineExecutionResult { ExitCode = 0 });
            });

        await cts.CancelAsync();

        // When cancellation is already requested, the method should propagate OperationCanceledException
        var action = async () => await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessWithTimeoutExceptionAsync()
    {
        var cts = new CancellationTokenSource();
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = -1,
            })
            .Verifiable();

        await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest, cts.Token);

        this.commandLineMock.Verify();
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessWithTimeoutVariableTimeoutAsync()
    {
        var cts = new CancellationTokenSource();
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.environmentVarServiceMock
            .Setup(x => x.DoesEnvironmentVariableExist(MavenCommandService.MvnCLIFileLevelTimeoutSecondsEnvVar))
            .Returns(true);

        this.environmentVarServiceMock
            .Setup(x => x.GetEnvironmentVariable(MavenCommandService.MvnCLIFileLevelTimeoutSecondsEnvVar))
            .Returns("0")
            .Callback(() => cts.Cancel());

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.Is<CancellationToken>(x => x.IsCancellationRequested),
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = -1,
            })
            .Verifiable();

        await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest, cts.Token);

        this.commandLineMock.Verify();
        this.environmentVarServiceMock.Verify();
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessWithRandomExceptionAsync()
    {
        var cts = new CancellationTokenSource();
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ThrowsAsync(new ArgumentNullException("Something Broke"))
            .Verifiable();

        var action = async () => await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest, cts.Token);
        await action.Should().ThrowAsync<ArgumentNullException>();

        this.commandLineMock.Verify();
    }

    [TestMethod]
    public void ParseDependenciesFile_Success()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        var content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine}";

        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            },
        };

        var lines = new[] { "com.bcde.test:top-level:jar:1.0.0", $"\\- {componentString}" };
        this.parserServiceMock.Setup(x => x.Parse(lines, It.IsAny<ISingleFileComponentRecorder>())).Verifiable();

        this.mavenCommandService.ParseDependenciesFile(processRequest);

        Mock.Verify(this.parserServiceMock);
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_FailedResult_NotCachedAsync()
    {
        // Arrange
        var pomLocation = "Test/location";
        var cliInvocationCount = 0;

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref cliInvocationCount);
                return new CommandLineExecutionResult { ExitCode = 1, StdErr = "Build failed" };
            });

        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream { Location = pomLocation },
            SingleFileComponentRecorder = new Mock<ISingleFileComponentRecorder>().Object,
        };

        // Act: First call - should fail
        var result1 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
        result1.Success.Should().BeFalse();
        cliInvocationCount.Should().Be(1);

        // Second call - should retry (not use cached failure)
        var result2 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
        result2.Success.Should().BeFalse();
        cliInvocationCount.Should().Be(2, "failed results should not be cached, allowing retries");
    }

    protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
    {
        result.Should().BeEquivalentTo(expected);
        return true;
    }
}
