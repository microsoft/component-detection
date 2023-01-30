﻿namespace Microsoft.ComponentDetection.Common.Telemetry.Records;
using System.Net;

public class PypiRetryTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "PypiRetry";

    /// <summary>
    /// Gets or sets the package Name (ex: pyyaml).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the set of dependency specifications that constrain the overall dependency request (ex: ==1.0, >=2.0).
    /// </summary>
    public string[] DependencySpecifiers { get; set; }

    /// <summary>
    /// Gets or sets the status code of the last failed call that caused the retry.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }
}
