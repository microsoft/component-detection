namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using PackageUrl;

public class NuGetComponent : TypedComponent
{
    private NuGetComponent()
    {
        /* Reserved for deserialization */
    }

    public NuGetComponent(string name, string version, string[] authors = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.NuGet));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.NuGet));
        this.Authors = authors;
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public string[] Authors { get; set; }

    public ISet<string> TargetFrameworks { get; set; } = new HashSet<string>();

    public override ComponentType Type => ComponentType.NuGet;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public override PackageURL PackageUrl => new PackageURL("nuget", null, this.Name, this.Version, null, null);
}
