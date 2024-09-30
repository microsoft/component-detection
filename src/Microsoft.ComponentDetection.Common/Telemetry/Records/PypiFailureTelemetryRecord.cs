namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System.Net;

public class PypiFailureTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PypiFailure";

    /// <summary>
    /// Gets or sets the package Name (ex: pyyaml).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the set of dependency specifications that constrain the overall dependency request (ex: ==1.0, >=2.0).
    /// </summary>
    public string[] DependencySpecifiers { get; set; }

    /// <summary>
    /// Gets or sets the status code of the last failed call.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }
}
