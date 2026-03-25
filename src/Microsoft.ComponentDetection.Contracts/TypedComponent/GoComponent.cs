#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;
using PackageUrl;

public class GoComponent : TypedComponent, IEquatable<GoComponent>
{
    public GoComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Go));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Go));
        this.Hash = string.Empty;
    }

    public GoComponent(string name, string version, string hash)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Go));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Go));
        this.Hash = this.ValidateRequiredInput(hash, nameof(this.Hash), nameof(ComponentType.Go));
    }

    public GoComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    // Commit should be used in place of version when available
    // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L610
    // The golang purl spec requires a namespace: https://github.com/package-url/purl-spec/blob/master/types/golang-definition.json
    [JsonPropertyName("packageUrl")]
    public override PackageUrl PackageUrl
    {
        get
        {
            var version = string.IsNullOrWhiteSpace(this.Hash) ? this.Version : this.Hash;
            var (ns, name) = this.GetNamespaceAndName();
            return new PackageUrl("golang", ns, name, version, null, null);
        }
    }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Go;

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} - {this.Type}";

    private (string Namespace, string Name) GetNamespaceAndName()
    {
        var lastSlash = this.Name.LastIndexOf('/');
        if (lastSlash > 0)
        {
            return (this.Name.Substring(0, lastSlash), this.Name.Substring(lastSlash + 1));
        }

        return (null, this.Name);
    }

    public override bool Equals(object obj)
    {
        return obj is GoComponent otherComponent && this.Equals(otherComponent);
    }

    public bool Equals(GoComponent other)
    {
        if (other == null)
        {
            return false;
        }

        return this.Name == other.Name && this.Version == other.Version && this.Hash == other.Hash;
    }

    public override int GetHashCode()
    {
        return this.Name.GetHashCode() ^ this.Version.GetHashCode() ^ this.Hash.GetHashCode();
    }
}
