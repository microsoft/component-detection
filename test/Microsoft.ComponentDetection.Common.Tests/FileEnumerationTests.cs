namespace Microsoft.ComponentDetection.Common.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.ComponentDetection.Contracts;
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
            Assert.Inconclusive("Test directory environment variable isn't set. Not testing");
        }

        var loggerMock = new Mock<ILogger>();

        var pathUtility = new PathUtilityService(loggerMock.Object);
        var sfe = new SafeFileEnumerable(new DirectoryInfo(Path.Combine(testDirectory, "root")), new[] { "*" }, loggerMock.Object, pathUtility, (name, directoryName) => false, true);

        var foundFiles = new List<string>();
        foreach (var f in sfe)
        {
            foundFiles.Add(f.File.FullName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.AreEqual(48, foundFiles.Count);
        }
        else
        {
            Assert.AreEqual(24, foundFiles.Count);
        }
    }
}
