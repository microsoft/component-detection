#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// A specific release of a project on pypy.
/// </summary>
public class PythonProjectRelease
{
    [JsonPropertyName("packagetype")]
    public string PackageType { get; set; }

    [JsonPropertyName("python_version")]
    public string PythonVersion { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; }

    [JsonPropertyName("url")]
    public Uri Url { get; set; }
}
