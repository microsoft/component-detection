#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipReportTypeTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipReportType";

    public bool PreGenerated { get; set; }

    public string FilePath { get; set; }

    public int PackageCount { get; set; }
}
