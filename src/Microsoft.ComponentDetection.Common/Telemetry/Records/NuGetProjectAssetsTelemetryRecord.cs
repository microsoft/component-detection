using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class NuGetProjectAssetsTelemetryRecord : IDetectionTelemetryRecord, IDisposable
    {
        public string RecordName => "NuGetProjectAssets";

        public string FoundTargets { get; set; }

        public string UnhandledException { get; set; }

        public string Frameworks { get; set; }

        private bool disposedValue = false;

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
                    TelemetryRelay.Instance.PostTelemetryRecord(this);
                }

                this.disposedValue = true;
            }
        }
    }
}
