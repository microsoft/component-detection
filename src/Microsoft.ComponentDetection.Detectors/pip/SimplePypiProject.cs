#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// A project from the new simple pypi api.
/// </summary>
public sealed record SimplePypiProject
{
    [JsonPropertyName("files")]
    public IList<SimplePypiProjectRelease> Files { get; init; }
}
