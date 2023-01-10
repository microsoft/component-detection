namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DockerServiceInspectImageTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceInspectImage";

    public string Image { get; set; }

    public string BaseImageDigest { get; set; }

    public string BaseImageRef { get; set; }

    public string ImageInspectResponse { get; set; }

    public string ExceptionMessage { get; set; }
}
