#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class CommandLineInvocationServiceTests
{
    private CommandLineInvocationService commandLineService;

    [TestInitialize]
    public void TestInitialize()
    {
        this.commandLineService = new CommandLineInvocationService();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ShowsCmdExeAsExecutableAsync()
    {
        var result = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        result.Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task FallbackWorksIfBadCommandsAreFirstAsync()
    {
        var result = await this.commandLineService.CanCommandBeLocatedAsync("57AB44A4-885A-47F4-866C-41417133B983", ["fakecommandexecutable.exe", "cmd.exe"], "/C");
        result.Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReturnsFalseIfNoValidCommandIsFoundAsync()
    {
        var result = await this.commandLineService.CanCommandBeLocatedAsync("57AB44A4-885A-47F4-866C-41417133B983", ["fakecommandexecutable.exe"], "/C");
        result.Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReturnsStandardOutputAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        isLocated.Should().BeTrue();
        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, "/C echo Expected Output");
        taskResult.ExitCode.Should().Be(0);
        taskResult.StdErr.Should().Be(string.Empty);
        taskResult.StdOut.Replace(Environment.NewLine, string.Empty).Should().Be("Expected Output");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecutesCommandEvenWithLargeStdOutAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        isLocated.Should().BeTrue();
        var largeStringBuilder = new StringBuilder();

        // Cmd.exe command limit is in the 8100s
        while (largeStringBuilder.Length < 8100)
        {
            largeStringBuilder.Append("Some sample text");
        }

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, $"/C echo {largeStringBuilder}");
        taskResult.ExitCode.Should().Be(0);
        taskResult.StdErr.Should().Be(string.Empty);
        taskResult.StdOut.Length.Should()
            .BeGreaterThan(
                8099,
                taskResult.StdOut.Length < 100 ? $"Stdout was '{taskResult.StdOut}', which is shorter than 8100 chars" : $"Length was {taskResult.StdOut.Length}, which is less than 8100");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecutesCommandCapturingErrorOutputAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        isLocated.Should().BeTrue();
        var largeStringBuilder = new StringBuilder();

        // Pick a command that is "too big" for cmd.
        while (largeStringBuilder.Length < 9000)
        {
            largeStringBuilder.Append("Some sample text");
        }

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, $"/C echo {largeStringBuilder}");
        taskResult.ExitCode.Should().Be(1);
        taskResult.StdErr.Should().Contain("too long", $"Expected '{taskResult.StdErr}' to contain 'too long'");
        taskResult.StdOut.Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecutesInAWorkingDirectoryAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        isLocated.Should().BeTrue();
        var tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempDirectory = Directory.CreateDirectory(tempDirectoryPath);

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");
        taskResult.ExitCode.Should().Be(0);
        taskResult.StdOut.Should().Contain(tempDirectoryPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ThrowsIfWorkingDirectoryDoesNotExistAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        isLocated.Should().BeTrue();

        var tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        Func<Task> action = async () => await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("ExecuteCommandAsync was called with a working directory that could not be located: *");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ColdCommand_ProbesOnceAndReusesTheCachedCommandAsync()
    {
        var marker = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var result = await this.commandLineService.ExecuteCommandAsync("cmd.exe", workingDirectory: null, cancellationToken: CancellationToken.None, parameters: ["/D", "/C", $"echo invocation>>\"{marker}\""]);

            result.ExitCode.Should().Be(0);
            (await File.ReadAllLinesAsync(marker)).Should().Equal("invocation", "invocation");

            result = await this.commandLineService.ExecuteCommandAsync("cmd.exe", workingDirectory: null, cancellationToken: CancellationToken.None, parameters: ["/D", "/C", $"echo invocation>>\"{marker}\""]);

            result.ExitCode.Should().Be(0);
            (await File.ReadAllLinesAsync(marker)).Should().Equal("invocation", "invocation", "invocation");
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CachedCommand_ReturnsNonzeroExitAsync()
    {
        (await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C")).Should().BeTrue();
        var result = await this.commandLineService.ExecuteCommandAsync("cmd.exe", workingDirectory: null, cancellationToken: CancellationToken.None, parameters: ["/D", "/C", "exit 7"]);

        result.ExitCode.Should().Be(7);
    }

    [TestMethod]
    public async Task ExecuteCommand_PropagatesCancellationBeforeStartingAsync()
    {
        Func<Task> action = async () => await this.commandLineService.ExecuteCommandAsync("not-an-executable", workingDirectory: null, cancellationToken: new CancellationToken(true));

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task AvailabilityProbe_PropagatesCancellationBeforeStartingAsync()
    {
        Func<Task> action = async () => await this.commandLineService.CanCommandBeLocatedAsync("not-an-executable", null, null, new CancellationToken(true));

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteCommand_CancelsAvailabilityProbeAndCachedExecutionAsync(bool cached)
    {
        if (cached)
        {
            (await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C")).Should().BeTrue();
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Func<Task> action = async () => await this.commandLineService.ExecuteCommandAsync("cmd.exe", workingDirectory: null, cancellationToken: cancellation.Token, parameters: ["/D", "/C", "ping -n 30 127.0.0.1 >nul"]);

        await action.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AvailabilityProbe_CancelsARunningProcessAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Func<Task> action = async () => await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", null, null, cancellation.Token, "/D", "/C", "ping -n 30 127.0.0.1 >nul");

        await action.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}
