#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class InvalidParseVersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "InvalidParseVersion";

    public string DetectorId { get; set; }

    public string FilePath { get; set; }

    public string Version { get; set; }

    public string MaxVersion { get; set; }
}