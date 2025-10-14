#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

/// <summary>
/// Matches a subset of https://raw.githubusercontent.com/spdx/spdx-spec/v2.2.1/schemas/spdx-schema.json.
/// </summary>
public class VcpkgSBOM
{
    public Package[] Packages { get; set; }

    public string Name { get; set; }
}
