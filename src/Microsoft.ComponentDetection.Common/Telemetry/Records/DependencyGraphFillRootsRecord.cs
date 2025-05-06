namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DependencyGraphFillRootsRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DependencyGraphFillRootsRecord";

    public string DetectorId { get; set; }

    public int Count { get; set; }

    public int ComponentCount { get; set; }
}
