#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

/// <summary>A PHP runtime, extension, library, or Composer tool observed in the scan environment.</summary>
public sealed class PhpSdkComponent : TypedComponent
{
    public PhpSdkComponent()
    {
        /* Reserved for deserialization */
    }

    public PhpSdkComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.PhpSdk));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.PhpSdk));
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.PhpSdk;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl => new(
        "generic",
        null,
        this.Name,
        this.Version,
        new SortedDictionary<string, string> { { "type", "phpsdk" } },
        null);

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} - {this.Type}";
}
