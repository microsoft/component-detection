#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DockerServiceTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerService";

    public string Image { get; set; }

    public string Command { get; set; }

    public string Container { get; set; }

    public string Stdout { get; set; }

    public string Stderr { get; set; }
}
