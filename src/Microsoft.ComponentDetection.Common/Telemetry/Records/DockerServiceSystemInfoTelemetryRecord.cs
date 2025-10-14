#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DockerServiceSystemInfoTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceSystemInfo";

    public string SystemInfo { get; set; }

    public string ExceptionMessage { get; set; }
}
