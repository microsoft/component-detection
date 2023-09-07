namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using PackageUrl;

public class SpdxPackageComponent : TypedComponent
{
    public SpdxPackageComponent(string name, string version, string supplier, string copyrightText, string downloadLocation)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Spdx));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Spdx));
        this.Supplier = this.ValidateRequiredInput(supplier, nameof(this.Supplier), nameof(ComponentType.Spdx));
        this.CopyrightText = this.ValidateRequiredInput(copyrightText, nameof(this.CopyrightText), nameof(ComponentType.Spdx));
        this.DownloadLocation = this.ValidateRequiredInput(downloadLocation, nameof(this.DownloadLocation), nameof(ComponentType.Spdx));
    }

    public SpdxPackageComponent(string name, string version, string supplier, string copyrightText, string downloadLocation, string packageUrl)
        : this(name, version, supplier, copyrightText, downloadLocation) => this.PackageUrl = new PackageURL(packageUrl);

    private SpdxPackageComponent()
    {
        // reserved for deserialization
    }

    public string CopyrightText { get; }

    public string DownloadLocation { get; }

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public string Name { get; }

    public override PackageURL PackageUrl { get; }

    public string Supplier { get; }

    public override ComponentType Type => ComponentType.Spdx;

    public string Version { get; }
}
