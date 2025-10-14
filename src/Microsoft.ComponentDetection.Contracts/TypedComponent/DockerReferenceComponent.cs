#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class DockerReferenceComponent : TypedComponent
{
    public DockerReferenceComponent(string hash, string repository = null, string tag = null)
    {
        this.Digest = this.ValidateRequiredInput(hash, nameof(this.Digest), nameof(ComponentType.DockerReference));
        this.Repository = repository;
        this.Tag = tag;
    }

    public DockerReferenceComponent(DockerReference reference)
    {
    }

    private DockerReferenceComponent()
    {
        /* Reserved for deserialization */
    }

    public string Repository { get; set; }

    public string Digest { get; set; }

    public string Tag { get; set; }

    public string Domain { get; set; }

    public override ComponentType Type => ComponentType.DockerReference;

    public DockerReference FullReference
    {
        get
        {
            return DockerReference.CreateDockerReference(this.Repository, this.Domain, this.Digest, this.Tag);
        }
    }

    protected override string ComputeId() => $"{this.Repository} {this.Tag} {this.Digest}";
}
