namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class LinuxContainerDetectorMissingVersion : BaseDetectionTelemetryRecord
{
    public override string RecordName { get; } = "MissingVersion";

    public string? Distribution { get; set; }

    public string? Release { get; set; }

    public string[]? PackageNames { get; set; }
}
