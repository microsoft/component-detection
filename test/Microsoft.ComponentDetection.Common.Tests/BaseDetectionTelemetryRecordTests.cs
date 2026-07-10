#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BaseDetectionTelemetryRecordTests
{
    private Type[] recordTypes;

    [TestInitialize]
    public void Initialize()
    {
        // this only discovers types in a single assembly, since that's the current situation!
        this.recordTypes = typeof(BaseDetectionTelemetryRecord).Assembly.GetTypes()
            .Where(type => typeof(BaseDetectionTelemetryRecord).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .ToArray();
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
    public void NonDiagnosticRecord_IsAlwaysPosted_WhenDisposed()
    {
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var postedRecords = new List<IDetectionTelemetryRecord>();
        telemetryServiceMock.Setup(x => x.PostRecord(It.IsAny<IDetectionTelemetryRecord>()))
            .Callback<IDetectionTelemetryRecord>(postedRecords.Add);
        TelemetryRelay.Instance.Init([telemetryServiceMock.Object]);

        try
        {
            using (new NonDiagnosticTestRecord())
            {
            }

            postedRecords.Should().ContainSingle();
        }
        finally
        {
            TelemetryRelay.Instance.Init([]);
        }
    }

    [TestMethod]
    public void DiagnosticRecord_PostingIsGatedOnDiagnosticsFlag()
    {
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var postedRecords = new List<IDetectionTelemetryRecord>();
        telemetryServiceMock.Setup(x => x.PostRecord(It.IsAny<IDetectionTelemetryRecord>()))
            .Callback<IDetectionTelemetryRecord>(postedRecords.Add);
        TelemetryRelay.Instance.Init([telemetryServiceMock.Object]);

        try
        {
            using (new DiagnosticTestRecord())
            {
            }

            var diagnosticEnabled = (bool)typeof(BaseDetectionTelemetryRecord)
                .GetField("DiagnosticEnabled", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

            if (diagnosticEnabled)
            {
                postedRecords.Should().ContainSingle("diagnostic records should be posted when diagnostics are enabled");
            }
            else
            {
                postedRecords.Should().BeEmpty("diagnostic records should be suppressed when diagnostics are disabled");
            }
        }
        finally
        {
            TelemetryRelay.Instance.Init([]);
        }
    }

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
