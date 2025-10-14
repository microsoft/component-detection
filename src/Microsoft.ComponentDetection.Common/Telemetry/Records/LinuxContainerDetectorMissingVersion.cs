#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxContainerDetectorMissingVersion : BaseDetectionTelemetryRecord
{
    public override string RecordName { get; } = "MissingVersion";

    public string Distribution { get; set; }

    public string Release { get; set; }

    public string[] PackageNames { get; set; }
}