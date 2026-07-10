namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;

internal class DetectorExecutionTelemetryRecord : BaseDetectionTelemetryRecord
{
    private string? experimentalInformation;

    public override string RecordName => "DetectorExecution";

    public string? DetectorId { get; set; }

    public int? DetectedComponentCount { get; set; }

    public int? ExplicitlyReferencedComponentCount { get; set; }

    public int? ReturnCode { get; set; }

    public bool IsExperimental { get; set; }

    public string? ExperimentalInformation
    {
        get => this.experimentalInformation;
        set => this.experimentalInformation = DiagnosticEnabled ? value : this.TruncateToMaxLines(value);
    }

    public string? AdditionalTelemetryDetails { get; set; }

}
