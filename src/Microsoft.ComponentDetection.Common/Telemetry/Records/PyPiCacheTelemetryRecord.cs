using System.Net;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class PypiCacheTelemetryRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "PyPiCache";

        /// <summary>
        /// Total number of PyPi requests that hit the cache instead of PyPi APIs
        /// </summary>
        public int NumCacheHits { get; set; }

        /// <summary>
        /// Size of the PyPi cache at class destruction
        /// </summary>
        public int FinalCacheSize { get; set; }
    }
}
