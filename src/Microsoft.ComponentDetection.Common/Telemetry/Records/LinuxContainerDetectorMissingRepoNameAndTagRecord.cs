namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class LinuxContainerDetectorMissingRepoNameAndTagRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "MissingRepoNameAndTag";
}
