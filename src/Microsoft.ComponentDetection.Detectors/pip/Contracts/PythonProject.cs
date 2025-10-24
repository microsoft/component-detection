#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;

/// <summary>
/// A project on pypi.
/// </summary>
public class PythonProject
{
    public SortedDictionary<string, IList<PythonProjectRelease>> Releases { get; set; }

#nullable enable
    public PythonProjectInfo? Info { get; set; }
}
