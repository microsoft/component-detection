using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Orchestrator")]

namespace Microsoft.ComponentDetection.Common.Telemetry;

public static class TelemetryConstants
{
    private static Guid correlationId;

    public static Guid CorrelationId
    {
        get
        {
            if (correlationId == Guid.Empty)
            {
                correlationId = Guid.NewGuid();
            }

            return correlationId;
        }

        set // This is temporarily public, but once a process boundary exists it will be internal and initialized by Orchestrator in BCDE
        {
            correlationId = value;
        }
    }
}
