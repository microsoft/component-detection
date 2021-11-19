using System;
using System.Diagnostics;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public abstract class BaseDetectionTelemetryRecord : IDetectionTelemetryRecord
    {
        public abstract string RecordName { get; }

        [Metric]
        public TimeSpan? ExecutionTime { get; protected set; }

        private Stopwatch stopwatch = new Stopwatch();

        protected BaseDetectionTelemetryRecord()
        {
            stopwatch.Start();
        }

        public void StopExecutionTimer()
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
                ExecutionTime = stopwatch.Elapsed;
            }
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StopExecutionTimer();
                    TelemetryRelay.Instance.PostTelemetryRecord(this);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
