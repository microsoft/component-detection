using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class LinuxComponent : TypedComponent
{
    private LinuxComponent()
    {
        /* Reserved for deserialization */
    }

    public LinuxComponent(string distribution, string release, string name, string version)
    {
        this.Distribution = this.ValidateRequiredInput(distribution, nameof(this.Distribution), nameof(ComponentType.Linux));
        this.Release = this.ValidateRequiredInput(release, nameof(this.Release), nameof(ComponentType.Linux));
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Linux));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Linux));
    }

    public string Distribution { get; set; }

    public string Release { get; set; }

    public string Name { get; set; }

    public string Version { get; set; }

    public override ComponentType Type => ComponentType.Linux;

    public override string Id => $"{this.Distribution} {this.Release} {this.Name} {this.Version} - {this.Type}";

    public override PackageURL PackageUrl
    {
        get
        {
            string packageType = null;

            if (this.IsUbuntu() || this.IsDebian())
            {
                packageType = "deb";
            }
            else if (this.IsCentOS() || this.IsFedora() || this.IsRHEL())
            {
                packageType = "rpm";
            }

            if (packageType != null)
            {
                return new PackageURL(packageType, this.Distribution, this.Name, this.Version, null, null);
            }

            return null;
        }
    }

    private bool IsUbuntu()
    {
        return this.Distribution.ToUpperInvariant() == "UBUNTU";
    }

    private bool IsDebian()
    {
        return this.Distribution.ToUpperInvariant() == "DEBIAN";
    }

    private bool IsCentOS()
    {
        return this.Distribution.ToUpperInvariant() == "CENTOS";
    }

    private bool IsFedora()
    {
        return this.Distribution.ToUpperInvariant() == "FEDORA";
    }

    private bool IsRHEL()
    {
        return this.Distribution.ToUpperInvariant() == "RED HAT ENTERPRISE LINUX";
    }
}
