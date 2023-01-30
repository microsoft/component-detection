namespace Microsoft.ComponentDetection.Common.Tests;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.TestsUtilities;
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

    [SkipTestIfNotWindows]
    public async Task ShowsCmdExeAsExecutableAsync()
    {
        Assert.IsTrue(await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C"));
    }

    [SkipTestIfNotWindows]
    public async Task FallbackWorksIfBadCommandsAreFirstAsync()
    {
        Assert.IsTrue(await this.commandLineService.CanCommandBeLocatedAsync("57AB44A4-885A-47F4-866C-41417133B983", new[] { "fakecommandexecutable.exe", "cmd.exe" }, "/C"));
    }

    [SkipTestIfNotWindows]
    public async Task ReturnsFalseIfNoValidCommandIsFoundAsync()
    {
        Assert.IsFalse(await this.commandLineService.CanCommandBeLocatedAsync("57AB44A4-885A-47F4-866C-41417133B983", new[] { "fakecommandexecutable.exe" }, "/C"));
    }

    [SkipTestIfNotWindows]
    public async Task ReturnsStandardOutputAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        Assert.IsTrue(isLocated);
        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, "/C echo Expected Output");
        Assert.AreEqual(0, taskResult.ExitCode);
        Assert.AreEqual(string.Empty, taskResult.StdErr);
        Assert.AreEqual("Expected Output", taskResult.StdOut.Replace(Environment.NewLine, string.Empty));
    }

    [SkipTestIfNotWindows]
    public async Task ExecutesCommandEvenWithLargeStdOutAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        Assert.IsTrue(isLocated);
        var largeStringBuilder = new StringBuilder();

        // Cmd.exe command limit is in the 8100s
        while (largeStringBuilder.Length < 8100)
        {
            largeStringBuilder.Append("Some sample text");
        }

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, $"/C echo {largeStringBuilder}");
        Assert.AreEqual(0, taskResult.ExitCode);
        Assert.AreEqual(string.Empty, taskResult.StdErr);
        Assert.IsTrue(taskResult.StdOut.Length > 8099, taskResult.StdOut.Length < 100 ? $"Stdout was '{taskResult.StdOut}', which is shorter than 8100 chars" : $"Length was {taskResult.StdOut.Length}, which is less than 8100");
    }

    [SkipTestIfNotWindows]
    public async Task ExecutesCommandCapturingErrorOutputAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        Assert.IsTrue(isLocated);
        var largeStringBuilder = new StringBuilder();

        // Pick a command that is "too big" for cmd.
        while (largeStringBuilder.Length < 9000)
        {
            largeStringBuilder.Append("Some sample text");
        }

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, $"/C echo {largeStringBuilder}");
        Assert.AreEqual(1, taskResult.ExitCode);
        Assert.IsTrue(taskResult.StdErr.Contains("too long"), $"Expected '{taskResult.StdErr}' to contain 'too long'");
        Assert.AreEqual(string.Empty, taskResult.StdOut);
    }

    [SkipTestIfNotWindows]
    public async Task ExecutesInAWorkingDirectoryAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        Assert.IsTrue(isLocated);
        var tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempDirectory = Directory.CreateDirectory(tempDirectoryPath);

        var taskResult = await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");
        taskResult.ExitCode.Should().Be(0);
        taskResult.StdOut.Should().Contain(tempDirectoryPath);
    }

    [SkipTestIfNotWindows]
    public async Task ThrowsIfWorkingDirectoryDoesNotExistAsync()
    {
        var isLocated = await this.commandLineService.CanCommandBeLocatedAsync("cmd.exe", default, "/C");
        Assert.IsTrue(isLocated);

        var tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        Func<Task> action = async () => await this.commandLineService.ExecuteCommandAsync("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");

        await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("ExecuteCommandAsync was called with a working directory that could not be located: *");
    }
}
