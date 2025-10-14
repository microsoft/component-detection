#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipReportSkipTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipReportSkip";

    public string SkipReason { get; set; }

    public string DetectorId { get; set; }

    public int DetectorVersion { get; set; }
}
