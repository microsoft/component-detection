namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class GoGraphTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "GoGraph";

    public string ProjectRoot { get; set; }

    public bool IsGoAvailable { get; set; }

    public bool WasGraphSuccessful { get; set; }
}
