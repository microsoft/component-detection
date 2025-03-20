namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxScannerSyftTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxScannerSyftTelemetry";

    public string LinuxComponents { get; set; }

    public string Exception { get; set; }

    public int Mariner2ComponentsRemoved { get; set; }
}
