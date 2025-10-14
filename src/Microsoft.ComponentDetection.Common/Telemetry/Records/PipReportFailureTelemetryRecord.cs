#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipReportFailureTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipReportFailure";

    public int ExitCode { get; set; }

    public string StdErr { get; set; }
}
