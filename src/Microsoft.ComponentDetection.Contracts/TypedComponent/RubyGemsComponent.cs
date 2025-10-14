#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using PackageUrl;

public class RubyGemsComponent : TypedComponent
{
    private RubyGemsComponent()
    {
        /* Reserved for deserialization */
    }

    public RubyGemsComponent(string name, string version, string source = "")
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.RubyGems));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.RubyGems));
        this.Source = source;
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public string Source { get; set; }

    public override ComponentType Type => ComponentType.RubyGems;

    public override PackageURL PackageUrl => new PackageURL("gem", null, this.Name, this.Version, null, null);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
