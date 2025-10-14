#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxContainerDetectorMissingRepoNameAndTagRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "MissingRepoNameAndTag";
}