namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using Microsoft.ComponentDetection.Common.Telemetry.Attributes;

/// <summary>
/// Telemetry record for tracking Maven CLI file cleanup operations.
/// </summary>
public class MavenCliCleanupTelemetryRecord : BaseDetectionTelemetryRecord
{
    /// <inheritdoc/>
    public override string RecordName => "MavenCliCleanup";

    /// <summary>
    /// Gets or sets the number of files successfully cleaned up.
    /// </summary>
    [Metric]
    public int FilesCleanedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of files that failed to be cleaned up.
    /// </summary>
    [Metric]
    public int FilesFailedCount { get; set; }

    /// <summary>
    /// Gets or sets the source directory that was scanned for cleanup.
    /// </summary>
    public string? SourceDirectory { get; set; }
}
