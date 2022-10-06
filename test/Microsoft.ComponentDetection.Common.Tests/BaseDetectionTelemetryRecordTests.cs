using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Common.Tests
{
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
                Assert.IsNotNull(inst);

                var recordName = inst.RecordName;

                Assert.IsTrue(!string.IsNullOrEmpty(recordName), $"RecordName not set for {type.FullName}!");

                if (dic.ContainsKey(recordName))
                {
                    Assert.Fail($"Duplicate RecordName:`{recordName}` found for {type.FullName} and {dic[recordName].FullName}!");
                }
                else
                {
                    dic.Add(recordName, type);
                }
            }
        }

        [TestMethod]
        public void SerializableProperties()
        {
            var serializableTypes = new HashSet<Type>(new[]
            {
                typeof(string),
                typeof(string[]),
                typeof(bool),
                typeof(int),
                typeof(int?),
                typeof(TimeSpan?),
                typeof(HttpStatusCode),
            });

            foreach (var type in this.recordTypes)
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!serializableTypes.Contains(property.PropertyType) &&
                        Attribute.GetCustomAttribute(property.PropertyType, typeof(DataContractAttribute)) == null)
                    {
                        Assert.Fail(
                            $"Type {property.PropertyType} on {type.Name}.{property.Name} is not allowed! " +
                            "Add it to the list if it serializes properly to JSON!");
                    }
                }
            }
        }
    }
}
