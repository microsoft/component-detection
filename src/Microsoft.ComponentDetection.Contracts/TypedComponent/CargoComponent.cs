#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;
using PackageUrl;

public class CargoComponent : TypedComponent
{
    public CargoComponent()
    {
        // reserved for deserialization
    }

    public CargoComponent(string name, string version, string author = null, string license = null, string source = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Cargo));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Cargo));
        this.Author = author;
        this.License = license;
        this.Source = source;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

#nullable enable
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("source")]
    public string? Source { get; set; }
#nullable disable

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Cargo;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl => new PackageURL("cargo", string.Empty, this.Name, this.Version, null, string.Empty);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
