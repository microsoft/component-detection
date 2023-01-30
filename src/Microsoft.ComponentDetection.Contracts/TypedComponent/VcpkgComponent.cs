namespace Microsoft.ComponentDetection.Contracts.TypedComponent;
using PackageUrl;

public class VcpkgComponent : TypedComponent
{
    private VcpkgComponent()
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

    public string SPDXID { get; set; }

    public string Name { get; set; }

    public string DownloadLocation { get; set; }

    public string Triplet { get; set; }

    public string Version { get; set; }

    public string Description { get; set; }

    public int PortVersion { get; set; }

    public override ComponentType Type => ComponentType.Vcpkg;

    public override string Id
    {
        get
        {
            if (this.PortVersion > 0)
            {
                return $"{this.Name} {this.Version}#{this.PortVersion} - {this.Type}";
            }
            else
            {
                return $"{this.Name} {this.Version} - {this.Type}";
            }
        }
    }

    public override PackageURL PackageUrl
    {
        get
        {
            if (this.PortVersion > 0)
            {
                return new PackageURL($"pkg:vcpkg/{this.Name}@{this.Version}?port_version={this.PortVersion}");
            }
            else if (this.Version != null)
            {
                return new PackageURL($"pkg:vcpkg/{this.Name}@{this.Version}");
            }
            else
            {
                return new PackageURL($"pkg:vcpkg/{this.Name}");
            }
        }
    }
}
