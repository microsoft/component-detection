namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;

/// <summary>
/// A specific release of a project from the new simple pypi api.
/// </summary>
public sealed record SimplePypiProjectRelease
{
    public string FileName { get; init; }

    public double Size { get; init; }

    public Uri Url { get; init; }
}
