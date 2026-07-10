namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using System.Diagnostics;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
{
    private static readonly bool DiagnosticEnabled = string.Equals(Environment.GetEnvironmentVariable("agent_diagnostic"), "True", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("System_Debug"), "True", StringComparison.OrdinalIgnoreCase);

    private readonly Stopwatch stopwatch = new Stopwatch();

    private bool disposedValue;

    protected BaseDetectionTelemetryRecord() => this.stopwatch.Start();

    public abstract string RecordName { get; }

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
