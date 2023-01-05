namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DockerServiceTryPullImageTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceTryPullImage";

    public string Image { get; set; }

    public string CreateImageProgress { get; set; }

    public string ExceptionMessage { get; set; }
}
