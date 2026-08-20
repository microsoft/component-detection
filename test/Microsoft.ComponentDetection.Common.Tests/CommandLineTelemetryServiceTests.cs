#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class CommandLineTelemetryServiceTests
{
    private Mock<IFileWritingService> fileWritingServiceMock;
    private CommandLineTelemetryService serviceUnderTest;
    private string capturedJson;

    [TestInitialize]
    public void TestInitialize()
    {
        this.fileWritingServiceMock = new Mock<IFileWritingService>();

        // Capture the JSON written to file
        this.fileWritingServiceMock
            .Setup(x => x.WriteFile(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, content) => this.capturedJson = content);

        this.serviceUnderTest = new CommandLineTelemetryService(
            NullLogger<CommandLineTelemetryService>.Instance,
            this.fileWritingServiceMock.Object);
    }

    [TestMethod]
    public void PostRecord_MasksSensitiveUrlCredentials()
    {
        // Arrange
        var record = new TestTelemetryRecord
        {
            Command = "pip install --index-url https://user:password123@pypi.example.com/simple package",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().NotContain("user:password123");
        this.capturedJson.Should().Contain("https://******@pypi.example.com/simple");
    }

    [TestMethod]
    public void PostRecord_MasksMultipleSensitiveUrls()
    {
        // Arrange - multiple URLs in the same string should all be masked
        var record = new TestTelemetryRecord
        {
            Command = "pip install --index-url https://token@registry1.com/simple --extra-index-url https://secret@registry2.com/simple",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert - both credentials are masked, both URLs are preserved
        this.capturedJson.Should().NotContain("token");
        this.capturedJson.Should().NotContain("secret");
        this.capturedJson.Should().Contain("https://******@registry1.com/simple");
        this.capturedJson.Should().Contain("https://******@registry2.com/simple");
    }

    [TestMethod]
    public void PostRecord_PreservesNonSensitiveContent()
    {
        // Arrange
        var record = new TestTelemetryRecord
        {
            Command = "pip install package-name --verbose",
            Details = "Normal details without credentials",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().Contain("pip install package-name --verbose");
        this.capturedJson.Should().Contain("Normal details without credentials");
    }

    [TestMethod]
    public void PostRecord_MasksSensitiveInfoInNestedObjects()
    {
        // Arrange
        var record = new TestTelemetryRecordWithNestedData
        {
            Metadata = new Dictionary<string, string>
            {
                { "registry", "https://apikey:secretkey@private.registry.com" },
                { "normalKey", "normalValue" },
            },
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().NotContain("apikey:secretkey");
        this.capturedJson.Should().Contain("https://******@private.registry.com");
        this.capturedJson.Should().Contain("normalValue");
    }

    [TestMethod]
    public void PostRecord_MasksSensitiveInfoInArrays()
    {
        // Arrange
        var record = new TestTelemetryRecordWithArrays
        {
            Commands =
            [
                "pip install --index-url https://user1:pass1@registry1.com package1",
                "pip install --index-url https://user2:pass2@registry2.com package2",
            ],
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().NotContain("user1:pass1");
        this.capturedJson.Should().NotContain("user2:pass2");
        this.capturedJson.Should().Contain("https://******@registry1.com");
        this.capturedJson.Should().Contain("https://******@registry2.com");
    }

    [TestMethod]
    public void PostRecord_HandlesNullValues()
    {
        // Arrange
        var record = new TestTelemetryRecord
        {
            Command = null,
            Details = "Some details",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert - should not throw and should contain the non-null value
        this.capturedJson.Should().Contain("Some details");
    }

    [TestMethod]
    public void PostRecord_DoesNotRecordWhenDisabled()
    {
        // Arrange
        this.serviceUnderTest.SetMode(TelemetryMode.Disabled);
        var record = new TestTelemetryRecord
        {
            Command = "some command",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().Be("[]");
    }

    [TestMethod]
    public void PostRecord_AddsTimestampAndCorrelationId()
    {
        // Arrange
        var record = new TestTelemetryRecord
        {
            Command = "test",
        };

        // Act
        this.serviceUnderTest.PostRecord(record);
        this.serviceUnderTest.Flush();

        // Assert
        this.capturedJson.Should().Contain("Timestamp");
        this.capturedJson.Should().Contain("CorrelationId");
    }

    private class TestTelemetryRecord : IDetectionTelemetryRecord
    {
        public string RecordName => "TestRecord";

        public string Command { get; set; }

        public string Details { get; set; }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    private class TestTelemetryRecordWithNestedData : IDetectionTelemetryRecord
    {
        public string RecordName => "TestRecordWithNestedData";

        public Dictionary<string, string> Metadata { get; set; }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    private class TestTelemetryRecordWithArrays : IDetectionTelemetryRecord
    {
        public string RecordName => "TestRecordWithArrays";

        public string[] Commands { get; set; }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
