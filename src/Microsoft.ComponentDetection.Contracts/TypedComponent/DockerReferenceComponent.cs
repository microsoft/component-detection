#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;

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

    public DockerReferenceComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("repository")]
    public string Repository { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("domain")]
    public string Domain { get; set; }

    [JsonIgnore]
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
