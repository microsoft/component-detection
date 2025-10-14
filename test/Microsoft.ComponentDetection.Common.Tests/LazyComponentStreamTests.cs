#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LazyComponentStreamTests
{
    [TestMethod]
    public void LazyComponentStreamWorks()
    {
        var pattern = "test";
        var tmpFile = Path.GetTempFileName();

        var lcs = new LazyComponentStream(new FileInfo(tmpFile), pattern, Mock.Of<ILogger>());

        lcs.Pattern.Should().Be(pattern);
        lcs.Location.Should().Be(tmpFile);

        // The stream should be empty.
        lcs.Stream.Length.Should().Be(0);
    }

    [TestMethod]
    public void LazyComponentStream_HandlesUnauthorizedAccessExceptions()
    {
        var pattern = "test";

        // Create a new temporary dictionary
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);

        var lcs = new LazyComponentStream(new FileInfo(tmpDir), pattern, Mock.Of<ILogger>());

        lcs.Pattern.Should().Be(pattern);
        lcs.Location.Should().Be(tmpDir);

        // The stream should be empty.
        lcs.Stream.Length.Should().Be(0);
    }

    [TestMethod]
    public void LazyComponentStream_HandlesOtherExceptions()
    {
        var pattern = "test";

        // Create a new temporary dictionary, but don't create it.
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var lcs = new LazyComponentStream(new FileInfo(tmpDir), pattern, Mock.Of<ILogger>());

        lcs.Pattern.Should().Be(pattern);
        lcs.Location.Should().Be(tmpDir);

        // The stream should be empty.
        lcs.Stream.Length.Should().Be(0);
    }
}
