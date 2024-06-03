namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public abstract class VersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public string Version { get; set; }

    public string MaxVersion { get; set; }
}
