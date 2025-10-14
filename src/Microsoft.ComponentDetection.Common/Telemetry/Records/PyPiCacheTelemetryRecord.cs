namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PypiCacheTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PyPiCache";

    /// <summary>
    /// Gets or sets total number of PyPi requests that hit the cache instead of PyPi APIs.
    /// </summary>
    public int NumCacheHits { get; set; }

    /// <summary>
    /// Gets or sets the size of the PyPi cache at class destruction.
    /// </summary>
    public int FinalCacheSize { get; set; }
}
