#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Moq;

public static class TelemetryHelper
{
    public static async Task<IEnumerable<T>> ExecuteWhileCapturingTelemetryAsync<T>(Func<Task> codeToExecute)
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
        TelemetryRelay.Instance.Init([telemetryServiceMock.Object]);

        try
        {
            await codeToExecute();
        }
        catch
        {
            // ignored
        }

        return records.ToList();
    }
}
