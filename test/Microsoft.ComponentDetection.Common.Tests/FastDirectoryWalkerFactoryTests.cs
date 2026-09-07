#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class FastDirectoryWalkerFactoryTests
{
    private string temporaryDirectory;

    [TestInitialize]
    public void TestInitialize()
    {
        this.temporaryDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(this.temporaryDirectory);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(this.temporaryDirectory))
        {
            Directory.Delete(this.temporaryDirectory, true);
        }
    }

    [TestMethod]
    public async Task GetDirectoryScanner_CompletesWhenDiscoveredDirectoryDisappears()
    {
        var disappearingDirectory = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "disappearing"));
        await File.WriteAllTextAsync(Path.Combine(disappearingDirectory.FullName, "component.txt"), "content");

        var directoryWasDeleted = false;
        bool DeleteDiscoveredDirectory(ReadOnlySpan<char> directoryName, ReadOnlySpan<char> parentPath)
        {
            _ = directoryName;
            _ = parentPath;
            Directory.Delete(disappearingDirectory.FullName, true);
            directoryWasDeleted = true;
            return false;
        }

        var walker = new FastDirectoryWalkerFactory(
            Mock.Of<IPathUtilityService>(),
            Mock.Of<ILogger<FastDirectoryWalkerFactory>>());

        var completion = new TaskCompletionSource<IReadOnlyList<FileSystemInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = walker.GetDirectoryScanner(
            new DirectoryInfo(this.temporaryDirectory),
            new ConcurrentDictionary<string, bool>(),
            DeleteDiscoveredDirectory).Subscribe(new RecordingObserver(completion));

        var results = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        directoryWasDeleted.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetDirectoryScanner_PropagatesUnexpectedEnumerationError()
    {
        var parentDirectory = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "parent"));
        Directory.CreateDirectory(Path.Combine(parentDirectory.FullName, "nested"));
        var expectedException = new InvalidOperationException("Unexpected enumeration error");

        bool ThrowForNestedDirectory(ReadOnlySpan<char> directoryName, ReadOnlySpan<char> parentPath)
        {
            _ = parentPath;

            if (directoryName.SequenceEqual("nested"))
            {
                throw expectedException;
            }

            return false;
        }

        var logger = new Mock<ILogger<FastDirectoryWalkerFactory>>();
        var walker = new FastDirectoryWalkerFactory(
            Mock.Of<IPathUtilityService>(),
            logger.Object);

        var completion = new TaskCompletionSource<IReadOnlyList<FileSystemInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = walker.GetDirectoryScanner(
            new DirectoryInfo(this.temporaryDirectory),
            new ConcurrentDictionary<string, bool>(),
            ThrowForNestedDirectory).Subscribe(new RecordingObserver(completion));

        Func<Task> waitForCompletion = async () => await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var assertion = await waitForCompletion.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Should().BeSameAs(expectedException);
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString().Contains("Directory enumeration failed")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    private sealed class RecordingObserver(TaskCompletionSource<IReadOnlyList<FileSystemInfo>> completion) : IObserver<FileSystemInfo>
    {
        private readonly List<FileSystemInfo> results = [];

        public void OnCompleted() => completion.SetResult(this.results);

        public void OnError(Exception error) => completion.SetException(error);

        public void OnNext(FileSystemInfo value) => this.results.Add(value);
    }
}
