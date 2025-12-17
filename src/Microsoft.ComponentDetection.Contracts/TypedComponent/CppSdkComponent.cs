#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

/// <summary>
/// Represents a C++ SDK component.
/// </summary>
public class CppSdkComponent : TypedComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CppSdkComponent"/> class.
    /// </summary>
    public CppSdkComponent()
    {
        /* Reserved for deserialization */
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CppSdkComponent"/> class.
    /// </summary>
    /// <param name="name">The name of the component.</param>
    /// <param name="version">The version of the component.</param>
    public CppSdkComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.CppSdk));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.CppSdk));
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.CppSdk;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl
    {
        get
        {
            var qualifiers = new SortedDictionary<string, string>
            {
                { "type", "cppsdk" },
            };
            return new PackageURL("generic", null, this.Name, this.Version, qualifiers, null);
        }
    }

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
