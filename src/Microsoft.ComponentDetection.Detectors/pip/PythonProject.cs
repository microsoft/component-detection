namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// A project on pypi.
/// </summary>
public class PythonProject
{
    [JsonPropertyName("releases")]
    public IDictionary<string, IList<PythonProjectRelease>> Releases { get; set; }
}
