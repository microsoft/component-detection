using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class SafeFileEnumerableTests
    {
        private Mock<ILogger> loggerMock;

        private Mock<IPathUtilityService> pathUtilityServiceMock;

        private string temporaryDirectory;

        [TestInitialize]
        public void TestInitialize()
        {
            loggerMock = new Mock<ILogger>();
            pathUtilityServiceMock = new Mock<IPathUtilityService>();
            temporaryDirectory = GetTemporaryDirectory();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanupTemporaryDirectory(temporaryDirectory);
        }

        [TestMethod]
        public void GetEnumerator_WorksOverExpectedFiles()
        {
            var subDir = Directory.CreateDirectory(Path.Combine(temporaryDirectory, "SubDir"));
            string name = string.Format("{0}.txt", Guid.NewGuid());

            var file0 = Path.Combine(temporaryDirectory, name);
            var subFile0 = Path.Combine(temporaryDirectory, "SubDir", name);

            File.Create(file0).Close();
            File.Create(subFile0).Close();

            IEnumerable<string> searchPatterns = new List<string> { name };

            pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(It.IsAny<string>())).Returns<string>((s) => s);
            pathUtilityServiceMock.Setup(x => x.MatchesPattern(name, name)).Returns(true);

            var enumerable = new SafeFileEnumerable(new DirectoryInfo(temporaryDirectory), searchPatterns, loggerMock.Object, pathUtilityServiceMock.Object, (directoryName, span) => false, true);

            int filesFound = 0;
            foreach (var file in enumerable)
            {
                file.File.FullName.Should().BeOneOf(file0, subFile0);
                filesFound++;
            }

            filesFound.Should().Be(2);
        }

        [TestMethod]
        public void GetEnumerator_IgnoresSubDirectories()
        {
            var subDir = Directory.CreateDirectory(Path.Combine(temporaryDirectory, "SubDir"));
            string name = string.Format("{0}.txt", Guid.NewGuid());

            var file0 = Path.Combine(temporaryDirectory, name);

            File.Create(file0).Close();
            File.Create(Path.Combine(temporaryDirectory, "SubDir", name)).Close();

            IEnumerable<string> searchPatterns = new List<string> { name };

            pathUtilityServiceMock.Setup(x => x.MatchesPattern(name, name)).Returns(true);

            var enumerable = new SafeFileEnumerable(new DirectoryInfo(temporaryDirectory), searchPatterns, loggerMock.Object, pathUtilityServiceMock.Object, (directoryName, span) => false, false);

            int filesFound = 0;
            foreach (var file in enumerable)
            {
                file.File.FullName.Should().BeOneOf(file0);
                filesFound++;
            }

            filesFound.Should().Be(1);
        }

        [TestMethod]
        public void GetEnumerator_CallsSymlinkCode()
        {
            Assert.Inconclusive("Need actual symlinks to accurately test this");
            var subDir = Directory.CreateDirectory(Path.Combine(temporaryDirectory, "SubDir"));
            string name = string.Format("{0}.txt", Guid.NewGuid());
            File.Create(Path.Combine(temporaryDirectory, name)).Close();
            File.Create(Path.Combine(temporaryDirectory, "SubDir", name)).Close();

            IEnumerable<string> searchPatterns = new List<string> { name };

            var enumerable = new SafeFileEnumerable(new DirectoryInfo(temporaryDirectory), searchPatterns, loggerMock.Object, pathUtilityServiceMock.Object, (directoryName, span) => false, true);

            foreach (var file in enumerable)
            {
            }

            pathUtilityServiceMock.Verify(x => x.ResolvePhysicalPath(temporaryDirectory), Times.AtLeastOnce);
        }

        [TestMethod]
        public void GetEnumerator_DuplicatePathIgnored()
        {
            Assert.Inconclusive("Need actual symlinks to accurately test this");
            Environment.SetEnvironmentVariable("GovernanceSymlinkAwareMode", bool.TrueString, EnvironmentVariableTarget.Process);

            var subDir = Directory.CreateDirectory(Path.Combine(temporaryDirectory, "SubDir"));
            var fakeSymlink = Directory.CreateDirectory(Path.Combine(temporaryDirectory, "FakeSymlink"));
            string name = string.Format("{0}.txt", Guid.NewGuid());
            string canary = string.Format("{0}.txt", Guid.NewGuid());
            File.Create(Path.Combine(temporaryDirectory, name)).Close();
            File.Create(Path.Combine(temporaryDirectory, "SubDir", name)).Close();
            File.Create(Path.Combine(temporaryDirectory, "FakeSymlink", canary)).Close();

            pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(temporaryDirectory)).Returns(temporaryDirectory);
            pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(subDir.FullName)).Returns(subDir.FullName);
            pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(fakeSymlink.FullName)).Returns(subDir.FullName);

            IEnumerable<string> searchPatterns = new List<string> { name };

            var enumerable = new SafeFileEnumerable(new DirectoryInfo(temporaryDirectory), searchPatterns, loggerMock.Object, pathUtilityServiceMock.Object, (directoryName, span) => false, true);

            foreach (var file in enumerable)
            {
                file.File.FullName.Should().NotBe(Path.Combine(temporaryDirectory, "FakeSymlink", canary));
            }

            pathUtilityServiceMock.Verify(x => x.ResolvePhysicalPath(temporaryDirectory), Times.AtLeastOnce);
        }

        private string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private void CleanupTemporaryDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
                // Swallow
            }
        }
    }
}
