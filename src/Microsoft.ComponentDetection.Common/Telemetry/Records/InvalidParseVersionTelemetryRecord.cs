namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

internal class InvalidParseVersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "InvalidParseVersion";

    public string? DetectorId { get; set; }

    public string? FilePath { get; set; }

    public string? Version { get; set; }

    public string? MaxVersion { get; set; }
}
