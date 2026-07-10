namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.IO;

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
        set => this.experimentalInformation = DiagnosticEnabled ? value : this.TruncateToFirst10Lines(value);
    }

    public string? AdditionalTelemetryDetails { get; set; }

    private string? TruncateToFirst10Lines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = new System.Collections.Generic.List<string>();
        using (var reader = new StringReader(text))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (lines.Count >= 10)
                {
                    // More than 10 lines exist, truncate
                    return string.Join(Environment.NewLine, lines);
                }

                lines.Add(line);
            }
        }

        // EOF reached with <= 10 lines, return original
        return text;
    }
}
