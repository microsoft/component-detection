namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class DockerServiceSystemInfoTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceSystemInfo";

    public string? SystemInfo { get; set; }

    public string? ExceptionMessage { get; set; }
}
