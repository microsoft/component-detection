using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests
{
    public static class TelemetryHelper
    {
        public static IEnumerable<T> ExecuteWhileCapturingTelemetry<T>(Action codeToExecute)
            where T : class, IDetectionTelemetryRecord
        {
            var telemetryServiceMock = new Mock<ITelemetryService>();
            var records = new ConcurrentBag<T>();
            telemetryServiceMock.Setup(x => x.PostRecord(It.IsAny<IDetectionTelemetryRecord>()))
                .Callback<IDetectionTelemetryRecord>(record =>
                {
                    if (record is T asT)
                    {
                        records.Add(asT);
                    }
                });
            var oldServices = TelemetryRelay.TelemetryServices;
            TelemetryRelay.TelemetryServices = new[] { telemetryServiceMock.Object };
            try
            {
                codeToExecute();
            }
            finally
            {
                TelemetryRelay.TelemetryServices = oldServices;
            }

            return records.ToList();
        }
    }
}
