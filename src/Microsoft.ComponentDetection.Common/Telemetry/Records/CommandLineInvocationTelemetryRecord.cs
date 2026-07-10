namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

internal class CommandLineInvocationTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "CommandLineInvocation";

    public string? PathThatWasRan { get; set; }

    public string? Parameters { get; set; }

    public int? ExitCode { get; set; }

    public string? StandardError { get; set; }

    public string? UnhandledException { get; set; }

    internal void Track(CommandLineExecutionResult result, string path, string parameters)
    {
        this.ExitCode = result.ExitCode;
        var sanitizedError = result.StdErr?.RemoveSensitiveInformation();
        this.StandardError = DiagnosticEnabled ? sanitizedError : this.TruncateStandardErrorTo10Lines(sanitizedError);
        this.TrackCommon(path, parameters);
    }

    internal void Track(Exception ex, string path, string parameters)
    {
        this.ExitCode = -1;
        this.UnhandledException = ex.ToString().RemoveSensitiveInformation();
        this.TrackCommon(path, parameters);
    }

    private void TrackCommon(string path, string parameters)
    {
        this.PathThatWasRan = path;
        this.Parameters = parameters?.RemoveSensitiveInformation();
        this.StopExecutionTimer();
    }

    private string? TruncateStandardErrorTo10Lines(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return error;
        }

        var lines = new System.Collections.Generic.List<string>();
        using (var reader = new StringReader(error))
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
        return error;
    }
}
