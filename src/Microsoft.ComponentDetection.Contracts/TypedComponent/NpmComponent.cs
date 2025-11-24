#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using Microsoft.ComponentDetection.Contracts.Internal;
using PackageUrl;

public class NpmComponent : TypedComponent
{
    private NpmComponent()
    {
        /* Reserved for deserialization */
    }

    public NpmComponent(string name, string version, string hash = null, NpmAuthor author = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Npm));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Npm));
        this.Hash = hash; // Not required; only found in package-lock.json, not package.json
        this.Author = author;
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public string Hash { get; set; }

    public NpmAuthor Author { get; set; }

    public override ComponentType Type => ComponentType.Npm;

    public override PackageURL PackageUrl => new PackageURL("npm", null, this.Name, this.Version, null, null);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
