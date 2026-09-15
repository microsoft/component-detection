namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

/// <summary>
/// Telemetry record for individual Docker service operations.
/// Each step emits its own record, allowing identification of hung operations
/// by observing which step's record is missing.
/// </summary>
internal class DockerServiceStepTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DockerServiceStep";

    /// <summary>
    /// The step being performed (CreateContainer, AttachContainer, StartContainer, ReadOutput, RemoveContainer).
    /// </summary>
    public string? Step { get; set; }

    /// <summary>
    /// The container ID (for correlation across steps).
    /// </summary>
    public string? ContainerId { get; set; }

    /// <summary>
    /// The image being scanned.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// The command passed to the container.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Whether this step was cancelled due to timeout.
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Exception message if the step failed.
    /// </summary>
    public string? ExceptionMessage { get; set; }
}
