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

        await cts.CancelAsync();

        // When cancellation is already requested, the method should throw OperationCanceledException
        // instead of proceeding with the CLI execution
        var action = async () => await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();

        // Verify that the CLI was never invoked
        this.commandLineMock.Verify(
            x => x.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string[]>()),
            Times.Never());
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
    public async Task GenerateDependenciesFile_ConcurrentCalls_OnlyOneCliInvocationAsync()
    {
        // Arrange: Create a temp directory with a real deps file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var pomLocation = Path.Combine(tempDir, "pom.xml");
        var depsFilePath = Path.Combine(tempDir, "bcde.mvndeps");

        try
        {
            var cliInvocationCount = 0;
            var cliStartedEvent = new ManualResetEventSlim(false);
            var allowCliToCompleteEvent = new ManualResetEventSlim(false);

            var bcdeMvnFileName = "bcde.mvndeps";
            var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

            this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                    MavenCommandService.PrimaryCommand,
                    MavenCommandService.AdditionalValidCommands,
                    It.IsAny<CancellationToken>(),
                    It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
                .Returns(async () =>
                {
                    Interlocked.Increment(ref cliInvocationCount);
                    cliStartedEvent.Set();

                    // Simulate CLI execution time - wait until test allows completion
                    allowCliToCompleteEvent.Wait(TimeSpan.FromSeconds(5));

                    // Create the deps file (simulating what mvn does)
                    await File.WriteAllTextAsync(depsFilePath, "com.test:artifact:jar:1.0.0");

                    return new CommandLineExecutionResult { ExitCode = 0 };
                });

            // Act: Start two concurrent calls for the same pom.xml
            var processRequest1 = new ProcessRequest
            {
                ComponentStream = new ComponentStream { Location = pomLocation },
            };
            var processRequest2 = new ProcessRequest
            {
                ComponentStream = new ComponentStream { Location = pomLocation },
            };

            var task1 = Task.Run(() => this.mavenCommandService.GenerateDependenciesFileAsync(processRequest1));
            var task2 = Task.Run(() => this.mavenCommandService.GenerateDependenciesFileAsync(processRequest2));

            // Wait for the first CLI call to start
            cliStartedEvent.Wait(TimeSpan.FromSeconds(5));

            // Allow the CLI to complete
            allowCliToCompleteEvent.Set();

            // Wait for both tasks to complete
            var results = await Task.WhenAll(task1, task2);

            // Assert: Only one CLI invocation should have occurred
            cliInvocationCount.Should().Be(1, "only one CLI invocation should occur for concurrent calls to the same pom.xml");

            // Both results should indicate success
            results[0].Success.Should().BeTrue();
            results[1].Success.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            this.mavenCommandService.ClearCache();
        }
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_FileDeletedBetweenCallers_SecondCallerRerunsCliAsync()
    {
        // Arrange: Create a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var pomLocation = Path.Combine(tempDir, "pom.xml");
        var depsFilePath = Path.Combine(tempDir, "bcde.mvndeps");

        try
        {
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

                    // Create the deps file (simulating what mvn does)
                    File.WriteAllText(depsFilePath, "com.test:artifact:jar:1.0.0");

                    return new CommandLineExecutionResult { ExitCode = 0 };
                });

            var processRequest = new ProcessRequest
            {
                ComponentStream = new ComponentStream { Location = pomLocation },
            };

            // Act: First call - should invoke CLI
            var result1 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result1.Success.Should().BeTrue();
            cliInvocationCount.Should().Be(1);

            // Delete the deps file (simulating what the detector does after reading it)
            File.Delete(depsFilePath);

            // Second call - should re-invoke CLI because file was deleted
            var result2 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result2.Success.Should().BeTrue();
            cliInvocationCount.Should().Be(2, "CLI should be re-invoked when the deps file was deleted");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            this.mavenCommandService.ClearCache();
        }
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

        try
        {
            // Act: First call - should fail
            var result1 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result1.Success.Should().BeFalse();
            cliInvocationCount.Should().Be(1);

            // Second call - should retry (not use cached failure)
            var result2 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result2.Success.Should().BeFalse();
            cliInvocationCount.Should().Be(2, "failed results should not be cached, allowing retries");
        }
        finally
        {
            this.mavenCommandService.ClearCache();
        }
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessfulResult_IsCachedAsync()
    {
        // Arrange: Create a temp directory with a real deps file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var pomLocation = Path.Combine(tempDir, "pom.xml");
        var depsFilePath = Path.Combine(tempDir, "bcde.mvndeps");

        try
        {
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

                    // Create the deps file (simulating what mvn does)
                    File.WriteAllText(depsFilePath, "com.test:artifact:jar:1.0.0");

                    return new CommandLineExecutionResult { ExitCode = 0 };
                });

            var processRequest = new ProcessRequest
            {
                ComponentStream = new ComponentStream { Location = pomLocation },
            };

            // Act: First call - should invoke CLI
            var result1 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result1.Success.Should().BeTrue();
            cliInvocationCount.Should().Be(1);

            // Second call - should use cached result (file still exists)
            var result2 = await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);
            result2.Success.Should().BeTrue();
            cliInvocationCount.Should().Be(1, "successful results should be cached when file still exists");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            this.mavenCommandService.ClearCache();
        }
    }

    [TestMethod]
    public void ClearCache_DisposesAndClearsResources()
    {
        // This test verifies ClearCache doesn't throw and can be called multiple times
        this.mavenCommandService.ClearCache();
        this.mavenCommandService.ClearCache(); // Should not throw on second call
    }

    protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
    {
        result.Should().BeEquivalentTo(expected);
        return true;
    }
}
