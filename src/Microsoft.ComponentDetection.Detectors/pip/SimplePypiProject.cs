namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;

/// <summary>
/// A project from the new simple pypi api.
/// </summary>
public class SimplePypiProject
{
    public IList<SimplePypiProjectRelease> Files { get; set; }
}
