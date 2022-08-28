namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    using System;
    using System.Diagnostics;
    using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

    public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
    {
        public abstract string RecordName { get; }

        [Metric]
        public TimeSpan? ExecutionTime { get; protected set; }

        private Stopwatch stopwatch = new Stopwatch();

        protected BaseDetectionTelemetryRecord()
        {
            this.stopwatch.Start();
        }

        public void StopExecutionTimer()
        {
            if (this.stopwatch.IsRunning)
            {
                this.stopwatch.Stop();
                this.ExecutionTime = this.stopwatch.Elapsed;
            }
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.StopExecutionTimer();
                    TelemetryRelay.Instance.PostTelemetryRecord(this);
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
