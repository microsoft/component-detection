using System;
using System.Diagnostics;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
    {
        private Stopwatch stopwatch = new Stopwatch();

        private bool disposedValue = false;

        protected BaseDetectionTelemetryRecord()
        {
            this.stopwatch.Start();
        }

        public abstract string RecordName { get; }

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
        }

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
    }
}
