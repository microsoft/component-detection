namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DependencyGraphInnerRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DependencyGraphInnerRecord";

    public string DetectorId { get; set; }

    public string ComponentId { get; set; }

    public int Count { get; set; }
}
