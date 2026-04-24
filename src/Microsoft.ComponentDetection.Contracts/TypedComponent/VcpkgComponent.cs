#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

public class VcpkgComponent : TypedComponent
{
    public VcpkgComponent()
    {
        /* Reserved for deserialization */
    }

    public VcpkgComponent(string spdxid, string name, string version, string triplet = null, string portVersion = null, string description = null, string downloadLocation = null)
    {
        int.TryParse(portVersion, out var port);

        this.SPDXID = this.ValidateRequiredInput(spdxid, nameof(this.SPDXID), nameof(ComponentType.Vcpkg));
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Vcpkg));
        this.Version = version;
        this.PortVersion = port;
        this.Triplet = triplet;
        this.Description = description;
        this.DownloadLocation = downloadLocation;
    }

    [JsonPropertyName("spdxid")]
    public string SPDXID { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("downloadLocation")]
    public string DownloadLocation { get; set; }

    [JsonPropertyName("triplet")]
    public string Triplet { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("portVersion")]
    public int PortVersion { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Vcpkg;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl
    {
        get
        {
            var qualifiers = this.PortVersion > 0
                ? new SortedDictionary<string, string> { { "port_version", this.PortVersion.ToString() } }
                : null;

            return new PackageURL("vcpkg", null, this.Name, this.Version, qualifiers, null);
        }
    }

    protected override string ComputeBaseId()
    {
        var componentLocationPrefix = string.Empty;
        if (!string.IsNullOrWhiteSpace(this.DownloadLocation) && !this.DownloadLocation.Trim().Equals("NONE", System.StringComparison.InvariantCultureIgnoreCase))
        {
            componentLocationPrefix = $"{this.DownloadLocation} : ";
        }

        var componentPortVersionSuffix = " ";
        if (this.PortVersion > 0)
        {
            componentPortVersionSuffix = $"#{this.PortVersion} ";
        }

        return $"{componentLocationPrefix}{this.Name} {this.Version}{componentPortVersionSuffix}- {this.Type}";
    }
}
