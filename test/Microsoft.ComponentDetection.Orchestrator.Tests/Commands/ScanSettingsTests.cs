namespace Microsoft.ComponentDetection.Orchestrator.Tests.Commands;

using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ScanSettingsTests
{
    [TestMethod]
    public void Validate_ChecksNullSourceDirectory()
    {
        var settings = new ScanSettings();

        var result = settings.Validate();

        result.Successful.Should().BeFalse();
    }

    [TestMethod]
    public void Validate_ChecksSourceDirectoryExists()
    {
        var settings = new ScanSettings
        {
            SourceDirectory = new DirectoryInfo(Path.GetTempPath()),
        };

        var result = settings.Validate();

        result.Successful.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_FailIfSourceDirectoryDoesntExist()
    {
        var settings = new ScanSettings
        {
            SourceDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())),
        };

        var result = settings.Validate();

        result.Successful.Should().BeFalse();
    }

    [TestMethod]
    public void CanSerialize()
    {
        var settings = new ScanSettings
        {
            SourceDirectory = new DirectoryInfo(Path.GetTempPath()),
            Output = "C:\\",
            ManifestFile = new FileInfo(Path.GetTempFileName()),
            SourceFileRoot = new DirectoryInfo(Path.GetTempPath()),
        };

        var action = () => JsonSerializer.Serialize(settings);

        action.Should().NotThrow();
    }
}
