using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Pip;

/// <summary>
/// A project on pypi.
/// </summary>
public class PythonProject
{
    public Dictionary<string, IList<PythonProjectRelease>> Releases { get; set; }
}
