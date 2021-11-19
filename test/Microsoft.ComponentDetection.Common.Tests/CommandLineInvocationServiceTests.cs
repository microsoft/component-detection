using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class CommandLineInvocationServiceTests
    {
        private CommandLineInvocationService commandLineService;

        [TestInitialize]
        public void TestInitialize()
        {
            commandLineService = new CommandLineInvocationService();
        }

        [SkipTestIfNotWindows]
        public async Task ShowsCmdExeAsExecutable()
        {
            Assert.IsTrue(await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C"));
        }

        [SkipTestIfNotWindows]
        public async Task FallbackWorksIfBadCommandsAreFirst()
        {
            Assert.IsTrue(await commandLineService.CanCommandBeLocated("57AB44A4-885A-47F4-866C-41417133B983", new[] { "fakecommandexecutable.exe", "cmd.exe" }, "/C"));
        }

        [SkipTestIfNotWindows]
        public async Task ReturnsFalseIfNoValidCommandIsFound()
        {
            Assert.IsFalse(await commandLineService.CanCommandBeLocated("57AB44A4-885A-47F4-866C-41417133B983", new[] { "fakecommandexecutable.exe" }, "/C"));
        }

        [SkipTestIfNotWindows]
        public async Task ReturnsStandardOutput()
        {
            var isLocated = await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C");
            Assert.IsTrue(isLocated);
            var taskResult = await commandLineService.ExecuteCommand("cmd.exe", default, "/C echo Expected Output");
            Assert.AreEqual(0, taskResult.ExitCode);
            Assert.AreEqual(string.Empty, taskResult.StdErr);
            Assert.AreEqual("Expected Output", taskResult.StdOut.Replace(System.Environment.NewLine, string.Empty));
        }

        [SkipTestIfNotWindows]
        public async Task ExecutesCommandEvenWithLargeStdOut()
        {
            var isLocated = await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C");
            Assert.IsTrue(isLocated);
            StringBuilder largeStringBuilder = new StringBuilder();
            while (largeStringBuilder.Length < 8100) // Cmd.exe command limit is in the 8100s
            {
                largeStringBuilder.Append("Some sample text");
            }

            var taskResult = await commandLineService.ExecuteCommand("cmd.exe", default, $"/C echo {largeStringBuilder.ToString()}");
            Assert.AreEqual(0, taskResult.ExitCode);
            Assert.AreEqual(string.Empty, taskResult.StdErr);
            Assert.IsTrue(taskResult.StdOut.Length > 8099, taskResult.StdOut.Length < 100 ? $"Stdout was '{taskResult.StdOut}', which is shorter than 8100 chars" : $"Length was {taskResult.StdOut.Length}, which is less than 8100");
        }

        [SkipTestIfNotWindows]
        public async Task ExecutesCommandCapturingErrorOutput()
        {
            var isLocated = await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C");
            Assert.IsTrue(isLocated);
            StringBuilder largeStringBuilder = new StringBuilder();
            while (largeStringBuilder.Length < 9000) // Pick a command that is "too big" for cmd.
            {
                largeStringBuilder.Append("Some sample text");
            }

            var taskResult = await commandLineService.ExecuteCommand("cmd.exe", default, $"/C echo {largeStringBuilder.ToString()}");
            Assert.AreEqual(1, taskResult.ExitCode);
            Assert.IsTrue(taskResult.StdErr.Contains("too long"), $"Expected '{taskResult.StdErr}' to contain 'too long'");
            Assert.AreEqual(string.Empty, taskResult.StdOut);
        }

        [SkipTestIfNotWindows]
        public async Task ExecutesInAWorkingDirectory()
        {
            var isLocated = await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C");
            Assert.IsTrue(isLocated);
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var tempDirectory = Directory.CreateDirectory(tempDirectoryPath);

            var taskResult = await commandLineService.ExecuteCommand("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");
            taskResult.ExitCode.Should().Be(0);
            taskResult.StdOut.Should().Contain(tempDirectoryPath);
        }

        [SkipTestIfNotWindows]
        public async Task ThrowsIfWorkingDirectoryDoesNotExist()
        {
            var isLocated = await commandLineService.CanCommandBeLocated("cmd.exe", default, "/C");
            Assert.IsTrue(isLocated);

            var tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            Func<Task> action = async () => await commandLineService.ExecuteCommand("cmd.exe", default, workingDirectory: tempDirectory, "/C cd");

            await action.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("ExecuteCommand was called with a working directory that could not be located: *");
        }
    }
}
