namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
[SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Test method")]
public class ComponentStreamEnumerableTests
{
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.loggerMock = new Mock<ILogger>();
    }

    [TestMethod]
    public void GetEnumerator_WorksOverExpectedFiles()
    {
        var tempFileOne = Path.GetTempFileName();
        var tempFileTwo = Path.GetTempFileName();
        var enumerable = new ComponentStreamEnumerable(
            new[]
            {
                new MatchedFile
                {
                    File = new FileInfo(tempFileOne),
                    Pattern = "Some Pattern",
                },
                new MatchedFile
                {
                    File = new FileInfo(tempFileTwo),
                    Pattern = "Some Pattern",
                },
            },
            this.loggerMock.Object);

        enumerable.Count()
            .Should().Be(2);
        foreach (var file in enumerable)
        {
            file.Stream
                .Should().NotBeNull();
            file.Pattern
                .Should().NotBeNull();
            file.Location
                .Should().BeOneOf(tempFileOne, tempFileTwo);
        }
    }

    [TestMethod]
    public void GetEnumerator_LogsAndBreaksEnumerationWhenFileIsMissing()
    {
        var tempFileOne = Path.GetTempFileName();
        var tempFileTwo = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempFileThree = Path.GetTempFileName();
        File.Delete(tempFileTwo);
        this.loggerMock.Setup(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(tempFileTwo)),
            It.IsAny<IOException>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()));
        var enumerable = new ComponentStreamEnumerable(
            new[]
            {
                new MatchedFile
                {
                    File = new FileInfo(tempFileOne),
                    Pattern = "Some Pattern",
                },
                new MatchedFile
                {
                    File = new FileInfo(tempFileTwo),
                    Pattern = "Some Pattern",
                },
            },
            this.loggerMock.Object).ToList();

        enumerable.Count
            .Should().Be(1);

        this.loggerMock.VerifyAll();
    }
}
