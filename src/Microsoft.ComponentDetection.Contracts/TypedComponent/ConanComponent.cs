namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using PackageUrl;

public class ConanComponent : TypedComponent
{
    private ConanComponent()
    {
        // reserved for deserialization
    }

    public ConanComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conan));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conan));
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public override ComponentType Type => ComponentType.Conan;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public override PackageURL PackageUrl => new PackageURL("conan", string.Empty, this.Name, this.Version, null, string.Empty);
}
