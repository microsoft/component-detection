namespace Microsoft.ComponentDetection.Common.Telemetry;

using System;

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
