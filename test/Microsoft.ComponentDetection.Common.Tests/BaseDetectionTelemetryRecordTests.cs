#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
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
public class BaseDetectionTelemetryRecordTests
{
    private Type[] recordTypes;
    private bool originalDiagnosticEnabled;
    private Mock<ITelemetryService> telemetryServiceMock;
    private List<IDetectionTelemetryRecord> publishedRecords;

    [TestInitialize]
    public void Initialize()
    {
        // this only discovers types in a single assembly, since that's the current situation!
        this.recordTypes = typeof(BaseDetectionTelemetryRecord).Assembly.GetTypes()
            .Where(type => typeof(BaseDetectionTelemetryRecord).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .ToArray();

        this.originalDiagnosticEnabled = BaseDetectionTelemetryRecord.DiagnosticEnabled;

        this.publishedRecords = [];
        this.telemetryServiceMock = new Mock<ITelemetryService>();
        this.telemetryServiceMock
            .Setup(x => x.PostRecord(It.IsAny<IDetectionTelemetryRecord>()))
            .Callback<IDetectionTelemetryRecord>(this.publishedRecords.Add);

        TelemetryRelay.Instance.Init([this.telemetryServiceMock.Object]);
    }

    [TestCleanup]
    public void Cleanup()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = this.originalDiagnosticEnabled;
        TelemetryRelay.Instance.Init([]);
    }

    [TestMethod]
    public void UniqueRecordNames()
    {
        var dic = new Dictionary<string, Type>();

        foreach (var type in this.recordTypes)
        {
            var inst = Activator.CreateInstance(type) as IDetectionTelemetryRecord;
            inst.Should().NotBeNull();

            var recordName = inst.RecordName;

            recordName.Should().NotBeNullOrEmpty($"RecordName not set for {type.FullName}!");

            dic.Should().NotContainKey(recordName, $"Duplicate RecordName:{recordName} found for {type.FullName}!");

            dic.Add(recordName, type);
        }
    }

    [TestMethod]
    public void SerializableProperties()
    {
        var serializableTypes = new HashSet<Type>(
        [
            typeof(string),
            typeof(string[]),
            typeof(bool),
            typeof(int),
            typeof(int?),
            typeof(TimeSpan?),
            typeof(HttpStatusCode),
        ]);

        foreach (var type in this.recordTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (!serializableTypes.Contains(property.PropertyType))
                {
                    Attribute.GetCustomAttribute(property.PropertyType, typeof(DataContractAttribute)).Should().NotBeNull(
                        $"Type {property.PropertyType} on {type.Name}.{property.Name} is not allowed! " +
                        "Add it to the list if it serializes properly to JSON!");
                }
            }
        }
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
    public void NonDiagnosticRecord_IsPosted_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        using (new NonDiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().ContainSingle("regular records are always published regardless of DiagnosticEnabled");
    }

    [TestMethod]
    public void NonDiagnosticRecord_IsPosted_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        using (new NonDiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().ContainSingle("regular records are always published regardless of DiagnosticEnabled");
    }

    [TestMethod]
    public void DiagnosticRecord_NotPosted_WhenDiagnosticsDisabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = false;

        using (new DiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().BeEmpty("diagnostic records should not be published when DiagnosticEnabled=false");
    }

    [TestMethod]
    public void DiagnosticRecord_IsPosted_WhenDiagnosticsEnabled()
    {
        BaseDetectionTelemetryRecord.DiagnosticEnabled = true;

        using (new DiagnosticTestRecord())
        {
        }

        this.publishedRecords.Should().ContainSingle("diagnostic records should be published when DiagnosticEnabled=true");
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
    public void CommandLineInvocation_StandardError_Unchanged_WhenUnder10Lines()
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
    public void DetectorExecution_ExperimentalInformation_Unchanged_WhenUnder10Lines()
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

    private sealed class NonDiagnosticTestRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "NonDiagnosticTestRecord";
    }

    private sealed class DiagnosticTestRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "DiagnosticTestRecord";

        public override bool IsDiagnostic => true;
    }
}
