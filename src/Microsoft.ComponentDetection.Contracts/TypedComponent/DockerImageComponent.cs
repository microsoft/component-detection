#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;

public class DockerImageComponent : TypedComponent
{
    public DockerImageComponent(string hash, string name = null, string tag = null)
    {
        this.Digest = this.ValidateRequiredInput(hash, nameof(this.Digest), nameof(ComponentType.DockerImage));
        this.Name = name;
        this.Tag = tag;
    }

    private DockerImageComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    public override ComponentType Type => ComponentType.DockerImage;

    protected override string ComputeId() => $"{this.Name} {this.Tag} {this.Digest}";
}
