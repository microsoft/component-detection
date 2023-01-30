﻿namespace Microsoft.ComponentDetection.Common.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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
        this.loggerMock = new Mock<ILogger>();
        this.pathUtilityServiceMock = new Mock<IPathUtilityService>();
        this.temporaryDirectory = this.GetTemporaryDirectory();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.CleanupTemporaryDirectory(this.temporaryDirectory);
    }

    [TestMethod]
    public void GetEnumerator_WorksOverExpectedFiles()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "SubDir"));
        var name = string.Format("{0}.txt", Guid.NewGuid());

        var file0 = Path.Combine(this.temporaryDirectory, name);
        var subFile0 = Path.Combine(this.temporaryDirectory, "SubDir", name);

        File.Create(file0).Close();
        File.Create(subFile0).Close();

        IEnumerable<string> searchPatterns = new List<string> { name };

        this.pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(It.IsAny<string>())).Returns<string>((s) => s);
        this.pathUtilityServiceMock.Setup(x => x.MatchesPattern(name, name)).Returns(true);

        var enumerable = new SafeFileEnumerable(new DirectoryInfo(this.temporaryDirectory), searchPatterns, this.loggerMock.Object, this.pathUtilityServiceMock.Object, (directoryName, span) => false, true);

        var filesFound = 0;
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
        var subDir = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "SubDir"));
        var name = string.Format("{0}.txt", Guid.NewGuid());

        var file0 = Path.Combine(this.temporaryDirectory, name);

        File.Create(file0).Close();
        File.Create(Path.Combine(this.temporaryDirectory, "SubDir", name)).Close();

        IEnumerable<string> searchPatterns = new List<string> { name };

        this.pathUtilityServiceMock.Setup(x => x.MatchesPattern(name, name)).Returns(true);

        var enumerable = new SafeFileEnumerable(new DirectoryInfo(this.temporaryDirectory), searchPatterns, this.loggerMock.Object, this.pathUtilityServiceMock.Object, (directoryName, span) => false, false);

        var filesFound = 0;
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
        var subDir = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "SubDir"));
        var name = string.Format("{0}.txt", Guid.NewGuid());
        File.Create(Path.Combine(this.temporaryDirectory, name)).Close();
        File.Create(Path.Combine(this.temporaryDirectory, "SubDir", name)).Close();

        IEnumerable<string> searchPatterns = new List<string> { name };

        var enumerable = new SafeFileEnumerable(new DirectoryInfo(this.temporaryDirectory), searchPatterns, this.loggerMock.Object, this.pathUtilityServiceMock.Object, (directoryName, span) => false, true);

        foreach (var file in enumerable)
        {
        }

        this.pathUtilityServiceMock.Verify(x => x.ResolvePhysicalPath(this.temporaryDirectory), Times.AtLeastOnce);
    }

    [TestMethod]
    public void GetEnumerator_DuplicatePathIgnored()
    {
        Assert.Inconclusive("Need actual symlinks to accurately test this");
        Environment.SetEnvironmentVariable("GovernanceSymlinkAwareMode", bool.TrueString, EnvironmentVariableTarget.Process);

        var subDir = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "SubDir"));
        var fakeSymlink = Directory.CreateDirectory(Path.Combine(this.temporaryDirectory, "FakeSymlink"));
        var name = string.Format("{0}.txt", Guid.NewGuid());
        var canary = string.Format("{0}.txt", Guid.NewGuid());
        File.Create(Path.Combine(this.temporaryDirectory, name)).Close();
        File.Create(Path.Combine(this.temporaryDirectory, "SubDir", name)).Close();
        File.Create(Path.Combine(this.temporaryDirectory, "FakeSymlink", canary)).Close();

        this.pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(this.temporaryDirectory)).Returns(this.temporaryDirectory);
        this.pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(subDir.FullName)).Returns(subDir.FullName);
        this.pathUtilityServiceMock.Setup(x => x.ResolvePhysicalPath(fakeSymlink.FullName)).Returns(subDir.FullName);

        IEnumerable<string> searchPatterns = new List<string> { name };

        var enumerable = new SafeFileEnumerable(new DirectoryInfo(this.temporaryDirectory), searchPatterns, this.loggerMock.Object, this.pathUtilityServiceMock.Object, (directoryName, span) => false, true);

        foreach (var file in enumerable)
        {
            file.File.FullName.Should().NotBe(Path.Combine(this.temporaryDirectory, "FakeSymlink", canary));
        }

        this.pathUtilityServiceMock.Verify(x => x.ResolvePhysicalPath(this.temporaryDirectory), Times.AtLeastOnce);
    }

    private string GetTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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
