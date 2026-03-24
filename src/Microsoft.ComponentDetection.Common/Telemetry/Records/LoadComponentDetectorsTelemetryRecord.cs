namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class LoadComponentDetectorsTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LoadComponentDetectors";

    public string? DetectorIds { get; set; }
}
