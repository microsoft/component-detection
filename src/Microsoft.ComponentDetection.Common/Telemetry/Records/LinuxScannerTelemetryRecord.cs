namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxScannerTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxScannerTelemetry";

    public string ImageToScan { get; set; }

    public string ScanStdOut { get; set; }

    public string ScanStdErr { get; set; }

    public string FailedDeserializingScannerOutput { get; set; }

    public bool SemaphoreFailure { get; set; }

    public string ScannerVersion { get; set; }
}
