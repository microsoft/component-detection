#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
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
            [
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
            ],
            this.loggerMock.Object);

        enumerable.Should().HaveCount(2);
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
            [
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
            ],
            this.loggerMock.Object).ToList();

        enumerable.Should().ContainSingle();

        this.loggerMock.VerifyAll();
    }
}
