using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public interface IDetectionTelemetryRecord : IDisposable
    {
        /// <summary>
        /// Gets the name of the record to be logged.
        /// </summary>
        string RecordName { get; }
    }
}
