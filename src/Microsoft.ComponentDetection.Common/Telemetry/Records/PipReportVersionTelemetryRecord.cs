namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipReportVersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipReportVersion";

    public string Version { get; set; }

    public string MaxVersion { get; set; }
}
