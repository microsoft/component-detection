namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxScannerSyftTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxScannerSyftTelemetry";

    public string Components { get; set; }

    public string Exception { get; set; }

    public string ComponentsRemoved { get; set; }
}
