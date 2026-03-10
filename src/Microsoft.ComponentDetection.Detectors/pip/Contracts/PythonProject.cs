#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// A project on pypi.
/// </summary>
public class PythonProject
{
    [JsonPropertyName("releases")]
    public SortedDictionary<string, IList<PythonProjectRelease>> Releases { get; set; }

#nullable enable
    [JsonPropertyName("info")]
    public PythonProjectInfo? Info { get; set; }
}
