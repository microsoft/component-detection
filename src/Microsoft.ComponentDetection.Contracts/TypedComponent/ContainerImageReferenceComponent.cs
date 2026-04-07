#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;

public class ContainerImageReferenceComponent : TypedComponent
{
    public ContainerImageReferenceComponent(string hash, string repository = null, string tag = null)
    {
        this.Digest = this.ValidateRequiredInput(hash, nameof(this.Digest), nameof(ComponentType.ContainerImageReference));
        this.Repository = repository;
        this.Tag = tag;
    }

    public ContainerImageReferenceComponent(ContainerImageReference reference)
    {
    }

    public ContainerImageReferenceComponent()
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
    public override ComponentType Type => ComponentType.ContainerImageReference;

    public ContainerImageReference FullReference
    {
        get
        {
            return ContainerImageReference.CreateContainerImageReference(this.Repository, this.Domain, this.Digest, this.Tag);
        }
    }

    protected override string ComputeBaseId() => $"{this.Repository} {this.Tag} {this.Digest}";
}
