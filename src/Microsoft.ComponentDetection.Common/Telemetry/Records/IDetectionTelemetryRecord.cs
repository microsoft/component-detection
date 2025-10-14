#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;

public interface IDetectionTelemetryRecord : IDisposable
{
    /// <summary>
    /// Gets the name of the record to be logged.
    /// </summary>
    string RecordName { get; }
}
