namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;

/// <summary>
/// A specific release of a project from the new simple pypi api.
/// </summary>
public class SimplePypiProjectRelease
{
    public string FileName { get; set; }

    public double Size { get; set; }

    public Uri Url { get; set; }
}
