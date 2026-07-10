namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
{
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
}
