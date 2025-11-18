namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using PackageUrl;

/// <summary>
/// Represents an RPM package component.
/// </summary>
public class RpmComponent : TypedComponent
{
    private RpmComponent()
    {
        // Reserved for deserialization
    }

    public RpmComponent(
        string name,
        string version,
        string arch,
        string release,
        int? epoch = null,
        string sourceRpm = null,
        string vendor = null,
        string[] provides = null,
        string[] requires = null
    )
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Rpm));
        this.Version = this.ValidateRequiredInput(
            version,
            nameof(this.Version),
            nameof(ComponentType.Rpm)
        );
        this.Arch = this.ValidateRequiredInput(arch, nameof(this.Arch), nameof(ComponentType.Rpm));
        this.Release = this.ValidateRequiredInput(
            release,
            nameof(this.Release),
            nameof(ComponentType.Rpm)
        );
        this.Epoch = epoch;
        this.SourceRpm = sourceRpm;
        this.Vendor = vendor;
        this.Provides = provides ?? [];
        this.Requires = requires ?? [];
    }

    /// <summary>
    /// Gets the package name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the package version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets the package architecture (e.g., x86_64, aarch64, noarch).
    /// </summary>
    public string Arch { get; set; }

    /// <summary>
    /// Gets the package release string.
    /// </summary>
    public string Release { get; set; }

    /// <summary>
    /// Gets the package epoch (used for version comparison).
    /// </summary>
    public int? Epoch { get; set; }

    /// <summary>
    /// Gets the source RPM filename this package was built from.
    /// </summary>
    public string SourceRpm { get; set; }

    /// <summary>
    /// Gets the vendor or organization that packaged this component.
    /// </summary>
    public string Vendor { get; set; }

    /// <summary>
    /// Gets the list of capabilities this package provides.
    /// </summary>
    public string[] Provides { get; set; }

    /// <summary>
    /// Gets the list of capabilities this package requires.
    /// </summary>
    public string[] Requires { get; set; }

    /// <inheritdoc />
    public override ComponentType Type => ComponentType.Rpm;

    /// <inheritdoc />
    public override PackageURL PackageUrl
    {
        get
        {
            var qualifiers = new SortedDictionary<string, string> { ["arch"] = this.Arch };

            if (this.Epoch.HasValue)
            {
                qualifiers["epoch"] = this.Epoch.Value.ToString();
            }

            if (!string.IsNullOrEmpty(this.SourceRpm))
            {
                qualifiers["upstream"] = this.SourceRpm;
            }

            // Note: Namespace should be set by the detector based on distribution ID
            // For now, we'll use null and let the detector override if needed
            var version = $"{this.Version}-{this.Release}";
            return new PackageURL("rpm", null, this.Name, version, qualifiers, null);
        }
    }

    /// <inheritdoc />
    protected override string ComputeId()
    {
        var epochStr = this.Epoch.HasValue ? $"{this.Epoch}:" : string.Empty;
        return $"{this.Name}@{epochStr}{this.Version}-{this.Release}/{this.Arch} - {this.Type}";
    }
}
