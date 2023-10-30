namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PipInspectTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PipInspect";

    public bool PipNotFound { get; set; }

    public bool WasGraphSuccessful { get; set; }

    public bool WasGoCliDisabled { get; set; }

    public bool WasGoFallbackStrategyUsed { get; set; }

    public bool DidGoCliCommandFail { get; set; }

    public string GoCliCommandError { get; set; }

    public string GoModVersion { get; set; }
}
