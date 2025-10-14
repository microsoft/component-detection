#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;

public class NuGetProjectAssetsTelemetryRecord : IDetectionTelemetryRecord, IDisposable
{
    private bool disposedValue;

    public string RecordName => "NuGetProjectAssets";

    public string FoundTargets { get; set; }

    public string UnhandledException { get; set; }

    public string Frameworks { get; set; }

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
                TelemetryRelay.Instance.PostTelemetryRecord(this);
            }

            this.disposedValue = true;
        }
    }
}
