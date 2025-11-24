#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;
using PackageUrl;

public class LinuxComponent : TypedComponent
{
    public LinuxComponent(string distribution, string release, string name, string version, string license = null, string author = null)
    {
        this.Distribution = this.ValidateRequiredInput(distribution, nameof(this.Distribution), nameof(ComponentType.Linux));
        this.Release = this.ValidateRequiredInput(release, nameof(this.Release), nameof(ComponentType.Linux));
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Linux));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Linux));
        this.License = license;
        this.Author = author;
    }

    private LinuxComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("distribution")]
    public string Distribution { get; set; }

    [JsonPropertyName("release")]
    public string Release { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

#nullable enable
    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }
#nullable disable

    public override ComponentType Type => ComponentType.Linux;

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

    protected override string ComputeId() => $"{this.Distribution} {this.Release} {this.Name} {this.Version} - {this.Type}";

    private bool IsUbuntu()
    {
        return this.Distribution.Equals("UBUNTU", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDebian()
    {
        return this.Distribution.Equals("DEBIAN", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCentOS()
    {
        return this.Distribution.Equals("CENTOS", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFedora()
    {
        return this.Distribution.Equals("FEDORA", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRHEL()
    {
        return this.Distribution.Equals("RED HAT ENTERPRISE LINUX", StringComparison.OrdinalIgnoreCase);
    }
}
