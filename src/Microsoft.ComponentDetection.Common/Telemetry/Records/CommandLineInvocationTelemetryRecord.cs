namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

internal class CommandLineInvocationTelemetryRecord : BaseDetectionTelemetryRecord
{
    private static readonly bool DiagnosticEnabled = string.Equals(Environment.GetEnvironmentVariable("agent.diagnostic"), "True", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("System.Debug"), "True", StringComparison.OrdinalIgnoreCase);

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

        var lines = error.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        if (lines.Length <= 10)
        {
            return error;
        }

        return string.Join(Environment.NewLine, lines.Take(10));
    }
}
