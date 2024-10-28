namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class GoGraphTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "GoGraph";

    public string ProjectRoot { get; set; }

    public bool IsGoAvailable { get; set; }

    public bool WasGraphSuccessful { get; set; }

    public bool WasGoCliDisabled { get; set; }

    public bool WasGoFallbackStrategyUsed { get; set; }

    public bool DidGoCliCommandFail { get; set; }

    public string GoCliCommandError { get; set; }

    public string GoModVersion { get; set; }

    public string ExceptionMessage { get; set; }
}
