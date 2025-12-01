#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Matches a subset of https://raw.githubusercontent.com/spdx/spdx-spec/v2.2.1/schemas/spdx-schema.json.
/// </summary>
public class VcpkgSBOM
{
    [JsonPropertyName("packages")]
    public Package[] Packages { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}
