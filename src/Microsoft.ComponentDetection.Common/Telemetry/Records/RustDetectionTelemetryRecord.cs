namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class RustDetectionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "RustDetection";

    public string DetectionMode { get; set; }

    public int SkippedCargoTomlCount { get; set; }

    public int SkippedCargoLockCount { get; set; }

    public int TotalSkippedFiles { get; set; }

    public int ProcessedCargoTomlCount { get; set; }

    public int ProcessedCargoLockCount { get; set; }

    public int ProcessedSbomCount { get; set; }

    public int TotalProcessedFiles { get; set; }

    public int OwnershipMapPackageCount { get; set; }

    public bool OwnershipMapAvailable { get; set; }

    public string SkipRatio { get; set; }
}
