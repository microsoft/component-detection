#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class PypiMaxRetriesReachedTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PypiMaxRetriesReached";

    /// <summary>
    /// Gets or sets the package Name (ex: pyyaml).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the set of dependency specifications that constrain the overall dependency request (ex: ==1.0, >=2.0).
    /// </summary>
    public string[] DependencySpecifiers { get; set; }
}
