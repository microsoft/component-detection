#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

public class LinuxComponent : TypedComponent
{
    public LinuxComponent()
    {
        /* Reserved for deserialization */
    }

    public LinuxComponent(string distribution, string release, string name, string version, string license = null, string author = null)
    {
        this.Distribution = this.ValidateRequiredInput(distribution, nameof(this.Distribution), nameof(ComponentType.Linux));
        this.Release = this.ValidateRequiredInput(release, nameof(this.Release), nameof(ComponentType.Linux));
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Linux));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Linux));
        this.License = license;
        this.Author = author;
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

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Linux;

    [JsonPropertyName("packageUrl")]
    public override PackageUrl PackageUrl
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
            else if (this.IsAlpine())
            {
                packageType = "apk";
            }

            if (packageType != null)
            {
                var distroId = this.GetDistroId();
                var qualifiers = new SortedDictionary<string, string>
                {
                    { "distro", $"{distroId}-{this.Release}" },
                };

                return new PackageUrl(packageType, distroId, this.Name, this.Version, qualifiers, null);
            }

            return null;
        }
    }

    protected override string ComputeBaseId() => $"{this.Distribution} {this.Release} {this.Name} {this.Version} - {this.Type}";

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

    private bool IsAlpine()
    {
        return this.Distribution.Equals("ALPINE", StringComparison.OrdinalIgnoreCase);
    }

    private string GetDistroId()
    {
        if (this.IsRHEL())
        {
            return "redhat";
        }

        return this.Distribution.ToLowerInvariant();
    }
}
