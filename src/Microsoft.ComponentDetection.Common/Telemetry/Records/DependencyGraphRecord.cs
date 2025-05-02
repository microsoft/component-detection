namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DependencyGraphRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DependencyGraphRecord";

    public string DetectorId { get; set; }
}
