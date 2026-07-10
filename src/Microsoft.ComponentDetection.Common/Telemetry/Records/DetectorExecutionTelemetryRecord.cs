namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.Linq;

internal class DetectorExecutionTelemetryRecord : BaseDetectionTelemetryRecord
{
    private static readonly bool DiagnosticEnabled = string.Equals(Environment.GetEnvironmentVariable("AGENT_DIAGNOSTIC"), "True", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("SYSTEM_DEBUG"), "True", StringComparison.OrdinalIgnoreCase);

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
        set => this.experimentalInformation = DiagnosticEnabled ? value : this.TruncateToLast10Lines(value);
    }

    public string? AdditionalTelemetryDetails { get; set; }

    private string? TruncateToLast10Lines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        if (lines.Length <= 10)
        {
            return text;
        }

        return string.Join(Environment.NewLine, lines.Take(10));
    }
}
