#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LoadComponentDetectorsTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LoadComponentDetectors";

    public string DetectorIds { get; set; }
}
