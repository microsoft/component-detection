namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class DetectorExecutionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DetectorExecution";

    public string DetectorId { get; set; }

    public int? DetectedComponentCount { get; set; }

    public int? ExplicitlyReferencedComponentCount { get; set; }

    public int? ReturnCode { get; set; }

    public bool IsExperimental { get; set; }

    public string ExperimentalInformation { get; set; }

    public string AdditionalTelemetryDetails { get; set; }
}
