namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class SpdxPackageComponent : TypedComponent
{
    public SpdxPackageComponent(string name, string version, string supplier, string packageSource, string copyrightText)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Spdx));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Spdx));
        this.Supplier = this.ValidateRequiredInput(supplier, nameof(this.Supplier), nameof(ComponentType.Spdx));
        this.CopyrightText = this.ValidateRequiredInput(copyrightText, nameof(this.CopyrightText), nameof(ComponentType.Spdx));
        this.PackageSource = this.ValidateRequiredInput(packageSource, nameof(this.PackageSource), nameof(ComponentType.Spdx));
    }

    private SpdxPackageComponent()
    {
        // reserved for deserialization
    }

    public string CopyrightText { get; }

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public string Name { get; }

    public string PackageSource { get; }

    public string Supplier { get; }

    public override ComponentType Type => ComponentType.Spdx;

    public string Version { get; }
}
