namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class RustGraphTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "RustGraph";

    public string CargoTomlLocation { get; set; }

    public bool WasRustFallbackStrategyUsed { get; set; }

    public string FallbackReason { get; set; }

    public bool FallbackCargoLockFound { get; set; }

    public string FallbackCargoLockLocation { get; set; }

    public bool DidRustCliCommandFail { get; set; }

    public string RustCliCommandError { get; set; }
}
