namespace Microsoft.ComponentDetection.Common.Telemetry;

using Microsoft.ComponentDetection.Common.Telemetry.Records;

public interface ITelemetryService
{
    /// <summary>
    /// Post a record to the telemetry service.
    /// </summary>
    /// <param name="record">The telemetry record to post.</param>
    void PostRecord(IDetectionTelemetryRecord record);

    /// <summary>
    /// Flush all telemetry events from the queue (usually called on shutdown to clear the queue).
    /// </summary>
    void Flush();

    /// <summary>
    /// Sets the telemetry mode for the service.
    /// </summary>
    void SetMode(TelemetryMode mode);
}
