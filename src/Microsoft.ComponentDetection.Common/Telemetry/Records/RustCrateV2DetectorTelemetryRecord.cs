namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class RustCrateV2DetectorTelemetryRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "RustCrateV2MalformedDependencies";

        public string PackageInfo { get; set; }

        public string Dependencies { get; set; }
    }
}
