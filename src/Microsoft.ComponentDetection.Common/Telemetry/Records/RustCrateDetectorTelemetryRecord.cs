namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class RustCrateDetectorTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "RustCrateMalformedDependencies";

    public string PackageInfo { get; set; }

    public string Dependencies { get; set; }
}
