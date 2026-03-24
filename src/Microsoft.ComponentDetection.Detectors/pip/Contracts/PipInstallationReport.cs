#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// See https://pip.pypa.io/en/stable/reference/installation-report/#specification.
/// </summary>
public sealed record PipInstallationReport
{
    /// <summary>
    /// Version of the installation report specification. Currently 1, but will be incremented if the format changes.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; }

    /// <summary>
    /// Version of pip used to produce the report.
    /// </summary>
    [JsonPropertyName("pip_version")]
    public string PipVersion { get; set; }

    /// <summary>
    /// Distribution packages (to be) installed.
    /// </summary>
    [JsonPropertyName("install")]
    public PipInstallationReportItem[] InstallItems { get; set; }

    /// <summary>
    /// Environment metadata for the report. See https://peps.python.org/pep-0508/#environment-markers.
    /// </summary>
    [JsonPropertyName("environment")]
    public IDictionary<string, string> Environment { get; set; }
}
