#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using PackageUrl;

public class ConanComponent : TypedComponent
{
    private ConanComponent()
    {
        // reserved for deserialization
    }

    public ConanComponent(string name, string version, string previous, string packageId)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conan));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conan));
        this.Md5Hash = this.ValidateRequiredInput(previous, nameof(this.Md5Hash), nameof(ComponentType.Conan));
        this.Sha1Hash = this.ValidateRequiredInput(packageId, nameof(this.Sha1Hash), nameof(ComponentType.Conan));
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public string Md5Hash { get; set; }

    public string Sha1Hash { get; set; }

    public string PackageSourceURL => $"https://conan.io/center/recipes/{this.Name}?version={this.Version}";

    public override ComponentType Type => ComponentType.Conan;

    public override PackageURL PackageUrl => new PackageURL("conan", string.Empty, this.Name, this.Version, null, string.Empty);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
