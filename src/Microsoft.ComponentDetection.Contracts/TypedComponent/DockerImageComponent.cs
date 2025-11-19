#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class DockerImageComponent : TypedComponent
{
    private DockerImageComponent()
    {
        /* Reserved for deserialization */
    }

    public DockerImageComponent(string hash, string name = null, string tag = null)
    {
        this.Digest = this.ValidateRequiredInput(hash, nameof(this.Digest), nameof(ComponentType.DockerImage));
        this.Name = name;
        this.Tag = tag;
    }

    public string Name { get; set; }

    public string Digest { get; set; }

    public string Tag { get; set; }

    public override ComponentType Type => ComponentType.DockerImage;

    protected override string ComputeId() => $"{this.Name} {this.Tag} {this.Digest}";
}
