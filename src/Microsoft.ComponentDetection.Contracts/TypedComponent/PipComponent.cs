#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using PackageUrl;

public class PipComponent : TypedComponent
{
    private PipComponent()
    {
        /* Reserved for deserialization */
    }

    public PipComponent(string name, string version, string author = null, string license = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Pip));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Pip));
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

    public override ComponentType Type => ComponentType.Pip;

    public override PackageURL PackageUrl => new PackageURL("pypi", null, this.Name, this.Version, null, null);

    [SuppressMessage("Usage", "CA1308:Normalize String to Uppercase", Justification = "Casing cannot be overwritten.")]
    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}".ToLowerInvariant();
}
