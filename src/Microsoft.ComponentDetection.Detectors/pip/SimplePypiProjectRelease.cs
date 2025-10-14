#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// A specific release of a project from the new simple pypi api.
/// </summary>
public sealed record SimplePypiProjectRelease
{
    [JsonPropertyName("filename")]
    public string FileName { get; init; }

    [JsonPropertyName("size")]
    public double Size { get; init; }

    [JsonPropertyName("url")]
    public Uri Url { get; init; }
}
