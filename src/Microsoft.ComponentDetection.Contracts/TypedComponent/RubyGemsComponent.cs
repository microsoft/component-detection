#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;
using PackageUrl;

public class RubyGemsComponent : TypedComponent
{
    public RubyGemsComponent(string name, string version, string source = "")
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.RubyGems));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.RubyGems));
        this.Source = source;
    }

    private RubyGemsComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; }

    public override ComponentType Type => ComponentType.RubyGems;

    public override PackageURL PackageUrl => new PackageURL("gem", null, this.Name, this.Version, null, null);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
