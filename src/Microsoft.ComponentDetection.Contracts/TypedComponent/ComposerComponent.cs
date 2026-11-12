#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;
using PackageUrl;

/// <summary>A Composer package identified by its full vendor/package name and version.</summary>
public sealed class ComposerComponent : TypedComponent
{
    public ComposerComponent()
    {
        /* Reserved for deserialization */
    }

    public ComposerComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Composer));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Composer));
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Composer;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl
    {
        get
        {
            var separator = this.Name.IndexOf('/');
            if (separator <= 0 || separator == this.Name.Length - 1 || separator != this.Name.LastIndexOf('/'))
            {
                throw new InvalidOperationException("Composer package names must contain exactly one '/' separating a nonempty vendor and package name.");
            }

            return new PackageURL("composer", this.Name.Substring(0, separator), this.Name.Substring(separator + 1), this.Version, null, null);
        }
    }

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} - {this.Type}";
}
