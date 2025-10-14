#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class FailedParsingFileRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "FailedParsingFile";

    public string DetectorId { get; set; }

    public string FilePath { get; set; }

    public string ExceptionMessage { get; set; }

    public string StackTrace { get; set; }
}