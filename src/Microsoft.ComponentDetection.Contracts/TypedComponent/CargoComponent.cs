namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using Newtonsoft.Json;
using PackageUrl;

public class CargoComponent : TypedComponent
{
    private CargoComponent()
    {
        // reserved for deserialization
    }

    public CargoComponent(string name, string version, string author = null, string license = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Cargo));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Cargo));
        this.Author = author;
        this.License = license;
    }

    public string Name { get; set; }

    public string Version { get; set; }

#nullable enable
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Author { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? License { get; set; }
#nullable disable

    public override ComponentType Type => ComponentType.Cargo;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public override PackageURL PackageUrl => new PackageURL("cargo", string.Empty, this.Name, this.Version, null, string.Empty);
}
