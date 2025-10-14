#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
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

    private GoComponent()
    {
        /* Reserved for deserialization */
    }

    public string Name { get; set; }

    public string Version { get; set; }

    public string Hash { get; set; }

    // Commit should be used in place of version when available
    // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L610
    public override PackageURL PackageUrl => new PackageURL("golang", null, this.Name, string.IsNullOrWhiteSpace(this.Hash) ? this.Version : this.Hash, null, null);

    public override ComponentType Type => ComponentType.Go;

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";

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
