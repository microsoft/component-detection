#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
public class FileComponentDetectorWithCleanupTests
{
    private List<string> fileSystemMockDirectories;
    private List<string> fileSystemMockFiles;

    private string testDirectory;
    private string existingFilePath;
    private string existingDirectoryPath;

    private ProcessRequest processRequest;
    private Mock<ILogger> loggerMock;
    private Mock<IFileUtilityService> fileUtilityServiceMock;
    private Mock<IDirectoryUtilityService> directoryUtilityServiceMock;

    [TestInitialize]
    public void TestInitialize()
    {
        // setup a mock file system
        this.fileSystemMockDirectories = [];
        this.fileSystemMockFiles = [];

        // Get directory of current executing assembly
        this.testDirectory = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Resources", "filedetector");
        this.existingDirectoryPath = Path.Combine(this.testDirectory, "test.egg-info");
        this.existingFilePath = Path.Combine(this.testDirectory, "testing.pyc");

        var componentStreamMock = new Mock<IComponentStream>();
        componentStreamMock.SetupGet(c => c.Location).Returns(Path.Combine(this.testDirectory, "requirements.txt"));

        this.processRequest = new ProcessRequest
        {
            ComponentStream = componentStreamMock.Object,
        };

        this.loggerMock = new Mock<ILogger>();

        // add default file system files
        this.fileSystemMockDirectories.Add(this.testDirectory);
        this.fileSystemMockDirectories.Add(this.existingDirectoryPath);
        this.fileSystemMockFiles.Add(this.existingFilePath);

        // mock files
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.fileUtilityServiceMock
            .Setup(f => f.Exists(It.IsAny<string>()))
            .Returns<string>(path => this.fileSystemMockFiles.Contains(path));
        this.fileUtilityServiceMock
            .Setup(f => f.Delete(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                this.fileSystemMockFiles.Remove(path);
            });

        // mock directories
        this.directoryUtilityServiceMock = new Mock<IDirectoryUtilityService>();
        this.directoryUtilityServiceMock
            .Setup(d => d.Exists(It.IsAny<string>()))
            .Returns<string>(path => this.fileSystemMockDirectories.Contains(path));
        this.directoryUtilityServiceMock
            .Setup(d => d.Delete(It.IsAny<string>(), true))
            .Callback<string, bool>((path, recurse) =>
            {
                foreach (var file in this.fileSystemMockFiles.Where(f => f.StartsWith(path)).ToList())
                {
                    this.fileSystemMockFiles.Remove(file);
                }

                foreach (var directory in this.fileSystemMockDirectories.Where(d => d.StartsWith(path)).ToList())
                {
                    this.fileSystemMockDirectories.Remove(directory);
                }
            });
        this.directoryUtilityServiceMock
            .Setup(d => d.GetFilesAndDirectories(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int>()))
            .Returns<string, List<string>, int>((root, patterns, depth) =>
            {
                var files = new HashSet<string>(this.fileSystemMockFiles.Where(f => !f.Equals(root) && f.StartsWith(root) && patterns.Any(p => this.IsPatternMatch(f, p))));
                var directories = new HashSet<string>(this.fileSystemMockDirectories.Where(d => d.StartsWith(root) && patterns.Any(p => this.IsPatternMatch(d, p))));
                return (files, directories);
            });
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleansUpCreatedFile()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.pyc"], this.loggerMock.Object, fileUtilityService, directoryUtilityService);
        var createdFilePath = Path.Combine(this.testDirectory, "todelete.pyc");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            (process, args, token) =>
            {
                // creates a single file
                this.fileSystemMockFiles.Add(createdFilePath);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();
        fileUtilityService.Exists(createdFilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleansUpCreatedDirectory()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info"], this.loggerMock.Object, fileUtilityService, directoryUtilityService);
        var createdDirectory = Path.Combine(this.testDirectory, "todelete.egg-info");
        var createdFilePath = Path.Combine(createdDirectory, "todelete.txt");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            (process, args, token) =>
            {
                // creates a single directory with a file in it
                this.fileSystemMockDirectories.Add(createdDirectory);
                this.fileSystemMockFiles.Add(createdFilePath);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();
        directoryUtilityService.Exists(createdDirectory).Should().BeFalse();
        fileUtilityService.Exists(createdFilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleanUpDisabled()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = false;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info", "*.txt"], this.loggerMock.Object, fileUtilityService, directoryUtilityService);
        var createdDirectory = Path.Combine(this.testDirectory, "todelete.egg-info");
        var createdFilePath = Path.Combine(createdDirectory, "todelete.txt");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            (process, args, token) =>
            {
                // creates a single directory with a file in it
                this.fileSystemMockDirectories.Add(createdDirectory);
                this.fileSystemMockFiles.Add(createdFilePath);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert we don't clean up
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();
        directoryUtilityService.Exists(createdDirectory).Should().BeTrue();
        fileUtilityService.Exists(createdFilePath).Should().BeTrue();

        // Assert we don't even try to read items
        this.directoryUtilityServiceMock.Verify(
            d => d.GetFilesAndDirectories(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WithCleanupAsync_CleanUpComplex()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.egg-info", "*.pyc"], this.loggerMock.Object, fileUtilityService, directoryUtilityService);

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
            (process, args, token) =>
            {
                this.fileSystemMockDirectories.Add(createdDirectoryKeep1);
                this.fileSystemMockFiles.Add(createdFileKeep1);
                this.fileSystemMockDirectories.Add(createdDirectoryKeep2);
                this.fileSystemMockFiles.Add(createdFileKeep2);

                this.fileSystemMockDirectories.Add(createdDirectoryDelete1);
                this.fileSystemMockFiles.Add(createdFileDelete1);
                this.fileSystemMockFiles.Add(createdFileDelete2);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert we don't clean up
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();

        directoryUtilityService.Exists(createdDirectoryKeep1).Should().BeTrue();
        fileUtilityService.Exists(createdFileKeep1).Should().BeTrue();
        directoryUtilityService.Exists(createdDirectoryKeep2).Should().BeTrue();
        fileUtilityService.Exists(createdFileKeep2).Should().BeTrue();

        directoryUtilityService.Exists(createdDirectoryDelete1).Should().BeFalse();
        fileUtilityService.Exists(createdFileDelete1).Should().BeFalse();
        fileUtilityService.Exists(createdFileDelete2).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithCleanupAsync_NoCleanup_WhenCleanupPatternEmpty()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector([], this.loggerMock.Object, fileUtilityService, directoryUtilityService);
        var createdFilePath = Path.Combine(this.testDirectory, "todelete.pyc");

        // Act
        await fileComponentDetector.TestCleanupAsync(
            (process, args, token) =>
            {
                // creates a single file
                this.fileSystemMockFiles.Add(createdFilePath);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();
        fileUtilityService.Exists(createdFilePath).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithCleanupAsync_NoCleanup_WhenUnauthorized()
    {
        // Arrange
        var fileUtilityService = this.fileUtilityServiceMock.Object;
        var directoryUtilityService = this.directoryUtilityServiceMock.Object;
        CancellationToken cancellationToken = default;
        var detectorArgs = new Dictionary<string, string>();
        var cleanupCreatedFiles = true;
        var fileComponentDetector = new TestFileComponentDetector(["*.pyc"], this.loggerMock.Object, fileUtilityService, directoryUtilityService);
        var createdFilePath = Path.Combine(this.testDirectory, "todelete.pyc");
        this.directoryUtilityServiceMock
            .Setup(d => d.GetFilesAndDirectories(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int>()))
            .Throws(new UnauthorizedAccessException("Unauthorized"));

        // Act
        await fileComponentDetector.TestCleanupAsync(
            (process, args, token) =>
            {
                // creates a single file
                this.fileSystemMockFiles.Add(createdFilePath);
                return Task.CompletedTask;
            },
            this.processRequest,
            detectorArgs,
            cleanupCreatedFiles,
            cancellationToken).ConfigureAwait(false);

        // Assert
        directoryUtilityService.Exists(this.testDirectory).Should().BeTrue();
        fileUtilityService.Exists(this.existingFilePath).Should().BeTrue();
        fileUtilityService.Exists(createdFilePath).Should().BeTrue();
    }

    private bool IsPatternMatch(string path, string pattern)
    {
        return Regex.IsMatch(path, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
    }

    public class TestFileComponentDetector : FileComponentDetectorWithCleanup
    {
        public TestFileComponentDetector(List<string> cleanupPatterns, ILogger logger, IFileUtilityService fileUtilityService, IDirectoryUtilityService directoryUtilityService)
            : base(fileUtilityService, directoryUtilityService)
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
