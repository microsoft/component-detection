namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class NpmLockfileVersionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "NpmLockfileVersion";

    /// <summary>
    /// Gets or sets the lockfile version in the package-lock.json file.
    /// </summary>
    public int LockfileVersion { get; set; }
}
