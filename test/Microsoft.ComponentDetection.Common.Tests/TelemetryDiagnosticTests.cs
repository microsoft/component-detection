#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class TelemetryDiagnosticTests
{
    private bool originalDiagnosticEnabled;
    private Mock<ITelemetryService> telemetryServiceMock;
    private List<IDetectionTelemetryRecord> publishedRecords;

    [TestInitialize]
    public void TestInitialize()
    {
        this.originalDiagnosticEnabled = BaseDetectionTelemetryRecord.DiagnosticEnabled;

        this.publishedRecords = [];
        this.telemetryServiceMock = new Mock<ITelemetryService>();
        this.telemetryServiceMock
            .Setup(s => s.PostRecord(It.IsAny<IDetectionTelemetryRecord>()))
            .Callback<IDetectionTelemetryRecord>(r => this.publishedRecords.Add(r));

        TelemetryRelay.Instance.Init([this.telemetryServiceMock.Object]);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = this.originalDiagnosticEnabled;
        TelemetryRelay.Instance.Init([]);
    }

    [TestMethod]
    public void IsDiagnostic_DefaultsToFalse()
    {
        new CommandLineInvocationTelemetryRecord().IsDiagnostic.Should().BeFalse();
        new DetectorExecutionTelemetryRecord().IsDiagnostic.Should().BeFalse();
    }

    [TestMethod]
    public void IsDiagnostic_HasJsonIgnoreAttribute()
    {
        var property = typeof(BaseDetectionTelemetryRecord).GetProperty(nameof(BaseDetectionTelemetryRecord.IsDiagnostic));

        property.GetCustomAttribute<JsonIgnoreAttribute>().Should().NotBeNull(
            "IsDiagnostic should not be serialized into telemetry output");
    }

    [TestMethod]
    public void RegularRecord_Published_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        using (new CommandLineInvocationTelemetryRecord())
        {
        }

        this.publishedRecords.Should().HaveCount(1, "regular records are always published regardless of DiagnosticEnabled");
    }

    [TestMethod]
    public void RegularRecord_Published_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        using (new CommandLineInvocationTelemetryRecord())
        {
        }

        this.publishedRecords.Should().HaveCount(1, "regular records are always published regardless of DiagnosticEnabled");
    }

    [TestMethod]
    public void DiagnosticRecord_NotPublished_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        using (new DiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().BeEmpty("diagnostic records should not be published when DiagnosticEnabled=false");
    }

    [TestMethod]
    public void DiagnosticRecord_Published_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        using (new DiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().HaveCount(1, "diagnostic records should be published when DiagnosticEnabled=true");
    }

    [TestMethod]
    public void CommandLineInvocation_StandardError_TruncatedTo10Lines_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var record = new CommandLineInvocationTelemetryRecord();
        record.Track(new CommandLineExecutionResult { ExitCode = 1, StdErr = this.BuildLines(50) }, "/cmd", string.Empty);

        record.StandardError.Split(Environment.NewLine).Should().HaveCount(10);
        record.StandardError.Should().Contain("Line 1");
        record.StandardError.Should().Contain("Line 10");
        record.StandardError.Should().NotContain("Line 11");
    }

    [TestMethod]
    public void CommandLineInvocation_StandardError_NotTruncated_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        var record = new CommandLineInvocationTelemetryRecord();
        record.Track(new CommandLineExecutionResult { ExitCode = 1, StdErr = this.BuildLines(50) }, "/cmd", string.Empty);

        record.StandardError.Split(Environment.NewLine).Should().HaveCount(50);
    }

    [TestMethod]
    public void CommandLineInvocation_StandardError_NotTruncated_WhenUnder10Lines()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var fiveLines = this.BuildLines(5);
        var record = new CommandLineInvocationTelemetryRecord();
        record.Track(new CommandLineExecutionResult { ExitCode = 1, StdErr = fiveLines }, "/cmd", string.Empty);

        record.StandardError.Should().Be(fiveLines);
    }

    [TestMethod]
    public void CommandLineInvocation_StandardError_NullHandled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var record = new CommandLineInvocationTelemetryRecord();
        record.Track(new CommandLineExecutionResult { ExitCode = 0, StdErr = null }, "/cmd", string.Empty);

        record.StandardError.Should().BeNull();
    }

    [TestMethod]
    public void DetectorExecution_ExperimentalInformation_TruncatedTo10Lines_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var record = new DetectorExecutionTelemetryRecord { ExperimentalInformation = this.BuildLines(50) };

        record.ExperimentalInformation.Split(Environment.NewLine).Should().HaveCount(10);
        record.ExperimentalInformation.Should().Contain("Line 1");
        record.ExperimentalInformation.Should().Contain("Line 10");
        record.ExperimentalInformation.Should().NotContain("Line 11");
    }

    [TestMethod]
    public void DetectorExecution_ExperimentalInformation_NotTruncated_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        var record = new DetectorExecutionTelemetryRecord { ExperimentalInformation = this.BuildLines(50) };

        record.ExperimentalInformation.Split(Environment.NewLine).Should().HaveCount(50);
    }

    [TestMethod]
    public void DetectorExecution_ExperimentalInformation_NotTruncated_WhenUnder10Lines()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var fiveLines = this.BuildLines(5);
        var record = new DetectorExecutionTelemetryRecord { ExperimentalInformation = fiveLines };

        record.ExperimentalInformation.Should().Be(fiveLines);
    }

    [TestMethod]
    public void DetectorExecution_ExperimentalInformation_NullHandled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        var record = new DetectorExecutionTelemetryRecord { ExperimentalInformation = null };

        record.ExperimentalInformation.Should().BeNull();
    }

    private string BuildLines(int count) =>
        string.Join(Environment.NewLine, Enumerable.Range(1, count).Select(i => $"Line {i}"));

    private sealed class DiagnosticTestRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "DiagnosticTest";

        public override bool IsDiagnostic => true;
    }
}
