namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxContainerDetectorUnsupportedOs : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxContainerDetectorUnsupportedOs";

    public string Os { get; set; }
}
