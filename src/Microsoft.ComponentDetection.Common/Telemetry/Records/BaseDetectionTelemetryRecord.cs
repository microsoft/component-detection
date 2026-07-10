namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
{
    internal const int MaxNonDiagnosticLines = 10;

    private readonly Stopwatch stopwatch = new Stopwatch();

    private bool disposedValue;

    protected BaseDetectionTelemetryRecord() => this.stopwatch.Start();

    internal static bool DiagnosticEnabled { get; set; } = string.Equals(Environment.GetEnvironmentVariable("AGENT_DIAGNOSTIC"), "True", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("SYSTEM_DEBUG"), "True", StringComparison.OrdinalIgnoreCase);

    public abstract string RecordName { get; }

    [JsonIgnore]
    public virtual bool IsDiagnostic { get; }

    [Metric]
    public TimeSpan? ExecutionTime { get; protected set; }

    public void StopExecutionTimer()
    {
        if (this.stopwatch.IsRunning)
        {
            this.stopwatch.Stop();
            this.ExecutionTime = this.stopwatch.Elapsed;
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.StopExecutionTimer();
                if (!this.IsDiagnostic || DiagnosticEnabled)
                {
                    TelemetryRelay.Instance.PostTelemetryRecord(this);
                }
            }

            this.disposedValue = true;
        }
    }

    protected static string? TruncateToMaxLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = new List<string>();
        using (var reader = new StringReader(text))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (lines.Count >= MaxNonDiagnosticLines)
                {
                    return string.Join(Environment.NewLine, lines);
                }

                lines.Add(line);
            }
        }

        return text;
    }
}
