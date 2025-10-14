#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class FileEnumerationTests
{
    [TestMethod]
    public void CanListAllFiles()
    {
        var testDirectory = Environment.GetEnvironmentVariable("COMPONENT_DETECTION_SYMLINK_TEST");
        if (string.IsNullOrWhiteSpace(testDirectory))
        {
            // Test directory environment variable isn't set. Not testing
            return;
        }

        var loggerMock = new Mock<ILogger<PathUtilityService>>();

        var pathUtility = new PathUtilityService(loggerMock.Object);
        var sfe = new SafeFileEnumerable(new DirectoryInfo(Path.Combine(testDirectory, "root")), ["*"], loggerMock.Object, pathUtility, (name, directoryName) => false, true);

        var foundFiles = new List<string>();
        foreach (var f in sfe)
        {
            foundFiles.Add(f.File.FullName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foundFiles.Should().HaveCount(48);
        }
        else
        {
            foundFiles.Should().HaveCount(24);
        }
    }
}
