#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using Newtonsoft.Json;

/// <summary>
/// A specific release of a project on pypy.
/// </summary>
public class PythonProjectRelease
{
    public string PackageType { get; set; }

    [JsonProperty("python_version")]
    public string PythonVersion { get; set; }

    public double Size { get; set; }

    public Uri Url { get; set; }
}
