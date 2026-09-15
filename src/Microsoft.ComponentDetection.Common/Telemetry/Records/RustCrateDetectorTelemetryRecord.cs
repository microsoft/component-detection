namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class RustCrateDetectorTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "RustCrateMalformedDependencies";

    public string? PackageInfo { get; set; }

    public string? Dependencies { get; set; }
}
