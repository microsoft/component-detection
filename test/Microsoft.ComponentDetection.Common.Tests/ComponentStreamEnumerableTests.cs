namespace Microsoft.ComponentDetection.Common.Tests;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComponentStreamEnumerableTests
{
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize() => this.loggerMock = new Mock<ILogger>();

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
        this.loggerMock.Setup(x => x.LogWarning(Match.Create<string>(message => message.Contains("not exist"))));
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
