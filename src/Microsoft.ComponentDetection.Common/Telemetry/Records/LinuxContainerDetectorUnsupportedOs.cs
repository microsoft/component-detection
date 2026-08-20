namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class LinuxContainerDetectorUnsupportedOs : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxContainerDetectorUnsupportedOs";

    public string? Os { get; set; }
}
