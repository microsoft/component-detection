#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class SimplePypiCacheTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "SimplePyPiCache";

    /// <summary>
    /// Gets or sets total number of PyPi project requests that hit the cache instead of SimplePyPi API.
    /// </summary>
    public int NumSimpleProjectCacheHits { get; set; }

    /// <summary>
    /// Gets or sets total number of project wheel file requests that hit the cache instead of API.
    /// </summary>
    public int NumProjectFileCacheHits { get; set; }

    /// <summary>
    /// Gets or sets the size of the Simple Project cache at class destruction.
    /// </summary>
    public int FinalSimpleProjectCacheSize { get; set; }

    /// <summary>
    /// Gets or sets the size of the Project Wheel File cache at class destruction.
    /// </summary>
    public int FinalProjectFileCacheSize { get; set; }
}
