namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class PubComponent : TypedComponent
{
    public PubComponent()
    {
        /* Reserved for deserialization */
    }

    public PubComponent(string name, string version, string dependency, string hash = null, string url = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Pub));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Pub));
        this.Dependency = dependency;
        this.Hash = hash; // Not required;
        this.Url = url;
    }

    public string Name { get; }

    public string Version { get; }

    public string Hash { get; }

    public string Url { get; }

    public string Dependency { get; }

    public override ComponentType Type => ComponentType.Pub;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    public override string ToString()
    {
        return $"Name={this.Name}\tVersion={this.Version}\tUrl={this.Url}";
    }

    protected bool Equals(PubComponent other) => this.Name == other.Name && this.Version == other.Version && this.Hash == other.Hash && this.Url == other.Url && this.Dependency == other.Dependency;

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((PubComponent)obj);
    }

    public override int GetHashCode() => this.Name.GetHashCode() ^ this.Version.GetHashCode() ^ this.Hash.GetHashCode() ^ this.Url.GetHashCode() ^ this.Dependency.GetHashCode();
}
