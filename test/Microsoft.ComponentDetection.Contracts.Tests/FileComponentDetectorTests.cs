namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class FileComponentDetectorTests
{
    private string testDirectory;
    private string existingFilePath;
    private string existingDirectoryPath;

    private ProcessRequest processRequest;
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        // Get directory of current executing assembly
        this.testDirectory = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Resources", "filedetector");
        this.existingDirectoryPath = Path.Combine(this.testDirectory, "test.egg-info");
        this.existingFilePath = Path.Combine(this.testDirectory, "testing.pyc");

        // recreate existing directory and file
        if (Directory.Exists(this.testDirectory))
        {
            Directory.Delete(this.testDirectory, true);
        }

        Directory.CreateDirectory(this.testDirectory);

        if (!File.Exists(this.existingFilePath))
        {
            File.WriteAllText(this.existingFilePath, "test");
        }

        var componentStreamMock = new Mock<IComponentStream>();
        componentStreamMock.SetupGet(c => c.Location).Returns(Path.Combine(this.testDirectory, "requirements.txt"));

        this.processRequest = new ProcessRequest
        {
            ComponentStream = componentStreamMock.Object,
        };

        this.loggerMock = new Mock<ILogger>();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleansUpCreatedFile()
    {
        // Arrange
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.pyc"], this.loggerMock.Object);
        var createdFilePath = Path.Combine(this.testDirectory, "todelete.pyc");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            async (process, args, token) =>
            {
                // creates a single file
                await File.WriteAllTextAsync(createdFilePath, "test", token).ConfigureAwait(false);
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        Directory.Exists(this.testDirectory).Should().BeTrue();
        File.Exists(this.existingFilePath).Should().BeTrue();
        File.Exists(createdFilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleansUpCreatedDirectory()
    {
        // Arrange
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info"], this.loggerMock.Object);
        var createdDirectory = Path.Combine(this.testDirectory, "todelete.egg-info");
        var createdFilePath = Path.Combine(createdDirectory, "todelete.txt");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            async (process, args, token) =>
            {
                // creates a single directory with a file in it
                Directory.CreateDirectory(createdDirectory);
                await File.WriteAllTextAsync(createdFilePath, "test", token).ConfigureAwait(false);
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        Directory.Exists(this.testDirectory).Should().BeTrue();
        File.Exists(this.existingFilePath).Should().BeTrue();
        Directory.Exists(createdDirectory).Should().BeFalse();
        File.Exists(createdFilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleanUpDisabled()
    {
        // Arrange
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = false;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info", "*.txt"], this.loggerMock.Object);
        var createdDirectory = Path.Combine(this.testDirectory, "todelete.egg-info");
        var createdFilePath = Path.Combine(createdDirectory, "todelete.txt");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            async (process, args, token) =>
            {
                // creates a single directory with a file in it
                Directory.CreateDirectory(createdDirectory);
                await File.WriteAllTextAsync(createdFilePath, "test", token).ConfigureAwait(false);
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert we don't clean up
        Directory.Exists(this.testDirectory).Should().BeTrue();
        File.Exists(this.existingFilePath).Should().BeTrue();
        Directory.Exists(createdDirectory).Should().BeTrue();
        File.Exists(createdFilePath).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleanUpComplex()
    {
        // Arrange
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info", "*.pyc"], this.loggerMock.Object);

        // creates following structure
        // - tokeep
        //   - tokeep.txt
        //   - todelete.egg-info
        //     - tokeep.txt
        // - tokeep.2
        //   - tokeep.py
        //   - todelete.pyx
        var createdDirectoryKeep1 = Path.Combine(this.testDirectory, "tokeep");
        var createdFileKeep1 = Path.Combine(createdDirectoryKeep1, "tokeep.txt");
        var createdDirectoryKeep2 = Path.Combine(this.testDirectory, "tokeep.2");
        var createdFileKeep2 = Path.Combine(createdDirectoryKeep2, "tokeep.py");

        var createdDirectoryDelete1 = Path.Combine(createdDirectoryKeep1, "todelete.egg-info");
        var createdFileDelete1 = Path.Combine(createdDirectoryDelete1, "tokeep.txt"); // should be deleted since it is in a directory that should be deleted
        var createdFileDelete2 = Path.Combine(createdDirectoryKeep2, "todelete.pyc");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            async (process, args, token) =>
            {
                Directory.CreateDirectory(createdDirectoryKeep1);
                await File.WriteAllTextAsync(createdFileKeep1, "test", token).ConfigureAwait(false);
                Directory.CreateDirectory(createdDirectoryKeep2);
                await File.WriteAllTextAsync(createdFileKeep2, "test", token).ConfigureAwait(false);

                Directory.CreateDirectory(createdDirectoryDelete1);
                await File.WriteAllTextAsync(createdFileDelete1, "test", token).ConfigureAwait(false);
                await File.WriteAllTextAsync(createdFileDelete2, "test", token).ConfigureAwait(false);
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert we don't clean up
        Directory.Exists(this.testDirectory).Should().BeTrue();
        File.Exists(this.existingFilePath).Should().BeTrue();

        Directory.Exists(createdDirectoryKeep1).Should().BeTrue();
        File.Exists(createdFileKeep1).Should().BeTrue();
        Directory.Exists(createdDirectoryKeep2).Should().BeTrue();
        File.Exists(createdFileKeep2).Should().BeTrue();

        Directory.Exists(createdDirectoryDelete1).Should().BeFalse();
        File.Exists(createdFileDelete1).Should().BeFalse();
        File.Exists(createdFileDelete2).Should().BeFalse();
    }

    public class TestFileComponentDetector : FileComponentDetector
    {
        public TestFileComponentDetector(List<string> cleanupPatterns, ILogger logger)
        {
            this.CleanupPatterns = cleanupPatterns;
            this.Logger = logger;
        }

        public override string Id => "TestFileComponentDetector";

        public override IList<string> SearchPatterns => ["requirements.txt"];

        public override IEnumerable<string> Categories => ["Test"];

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Pip];

        public override int Version { get; } = 1;

        public async Task TestCleanupAsync(
            Func<ProcessRequest, IDictionary<string, string>, CancellationToken, Task> process,
            ProcessRequest processRequest,
            IDictionary<string, string> detectorArgs,
            bool cleanupCreatedFiles,
            CancellationToken cancellationToken = default)
        {
            await this.WithCleanupAsync(
                process,
                processRequest,
                detectorArgs,
                cleanupCreatedFiles,
                cancellationToken);
        }

        protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
