namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DockerServiceImageExistsLocallyTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceImageExistsLocally";

    public string Image { get; set; }

    public string ImageInspectResponse { get; set; }

    public string ExceptionMessage { get; set; }
}
