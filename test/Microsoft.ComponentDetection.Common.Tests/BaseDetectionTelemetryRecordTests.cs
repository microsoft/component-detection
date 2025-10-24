#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            dic.Should().NotContainKey(recordName, "Duplicate RecordName:`{RecordName}` found for {TypeName}!", recordName, type.FullName);

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
}
