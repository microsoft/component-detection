namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipReportVersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipReportVersion";

    public int Version { get; set; }

    public int MaxVersion { get; set; }
}
