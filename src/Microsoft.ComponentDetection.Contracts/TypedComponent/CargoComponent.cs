using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class CargoComponent : TypedComponent
{
    private CargoComponent()
    {
        // reserved for deserialization
    }

    public CargoComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Cargo));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Cargo));
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public override ComponentType Type => ComponentType.Cargo;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public override PackageURL PackageUrl => new PackageURL("cargo", string.Empty, this.Name, this.Version, null, string.Empty);
}
