#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class FileWritingServiceTests
{
    private const string SampleObjectJson = @"{
  ""key1"": ""value1"",
  ""key2"": ""value2""
}";

    private static readonly IDictionary<string, string> SampleObject = new Dictionary<string, string>
    {
        { "key1", "value1" },
        { "key2", "value2" },
    };

    private FileWritingService serviceUnderTest;
    private string tempFolder;

    [TestInitialize]
    public void TestInitialize()
    {
        this.serviceUnderTest = new FileWritingService();

        // Get a temp file and repurpose it as a temp folder
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        Directory.CreateDirectory(tempFile);
        this.tempFolder = tempFile;

        this.serviceUnderTest.Init(this.tempFolder);
    }

    [TestCleanup]
    public void TestCleanup() => Directory.Delete(this.tempFolder, true);

    [TestMethod]
    public void AppendToFile_AppendsToFiles()
    {
        var relativeDir = "someOtherFileName.json";
        var fileLocation = Path.Combine(this.tempFolder, relativeDir);
        File.Create(fileLocation).Dispose();

        this.serviceUnderTest.AppendToFile(relativeDir, SampleObject);
        this.serviceUnderTest.Dispose();

        var text = File.ReadAllText(Path.Combine(this.tempFolder, relativeDir));
        text.Should().Be(SampleObjectJson);
    }

    [TestMethod]
    public void WriteFile_CreatesAFile()
    {
        var relativeDir = "someFileName.txt";
        this.serviceUnderTest.WriteFile(relativeDir, "sampleText");
        var text = File.ReadAllText(Path.Combine(this.tempFolder, relativeDir));
        text.Should().Be("sampleText");
    }

    [TestMethod]
    public void WriteFile_WritesJson()
    {
        var relativeDir = "someFileName.txt";
        var fileInfo = new FileInfo(Path.Combine(this.tempFolder, relativeDir));

        this.serviceUnderTest.WriteFile(fileInfo, SampleObject);

        var text = File.ReadAllText(Path.Combine(this.tempFolder, relativeDir));
        text.Should().Be(SampleObjectJson);
    }

    [TestMethod]
    public void WriteFile_AppendToFile_WorkWithTemplatizedPaths()
    {
        var relativeDir = "somefile_{timestamp}.txt";

        this.serviceUnderTest.WriteFile(relativeDir, "sampleText");
        this.serviceUnderTest.AppendToFile(relativeDir, SampleObject);
        this.serviceUnderTest.Dispose();

        var files = Directory.GetFiles(this.tempFolder);
        files.Should().NotBeEmpty();
        File.ReadAllText(files[0]).Should().Contain($"sampleText{SampleObjectJson}");
        this.VerifyTimestamp(files[0], "somefile_", ".txt");
    }

    [TestMethod]
    public void ResolveFilePath_ResolvedTemplatizedPaths()
    {
        var relativeDir = "someOtherFile_{timestamp}.txt";

        this.serviceUnderTest.WriteFile(relativeDir, string.Empty);

        var fullPath = this.serviceUnderTest.ResolveFilePath(relativeDir);
        this.VerifyTimestamp(fullPath, "someOtherFile_", ".txt");
    }

    [TestMethod]
    public void InitLogger_FailsOnDirectoryThatDoesNotExist()
    {
        var relativeDir = Guid.NewGuid();
        var actualServiceUnderTest = new FileWritingService();

        var action = () => actualServiceUnderTest.Init(Path.Combine(this.serviceUnderTest.BasePath, relativeDir.ToString()));

        action.Should().Throw<InvalidUserInputException>();
    }

    private void VerifyTimestamp(string fullPath, string prefix, string suffix)
    {
        var fileName = Path.GetFileName(fullPath);
        fileName
            .Should().StartWith(prefix)
            .And.EndWith(suffix);
        var timestamp = fileName.Substring(prefix.Length, FileWritingService.TimestampFormatString.Length);
        var dateTime = DateTime.ParseExact(timestamp, FileWritingService.TimestampFormatString, CultureInfo.InvariantCulture);
        dateTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
