#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class GoReplaceTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "GoReplace";

    public string GoModPathAndVersion { get; set; }

    public string GoModReplacement { get; set; }

    public string ExceptionMessage { get; set; }
}
