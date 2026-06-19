#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text.Json.Serialization;

public class CondaComponent : TypedComponent
{
    public CondaComponent(string name, string version, string build, string channel, string subdir, string @namespace, string url, string md5, string sha256 = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conda));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conda));
        this.Build = build;
        this.Channel = channel;
        this.Subdir = subdir;
        this.Namespace = @namespace;
        this.Url = url;
        this.MD5 = md5;
        this.Sha256 = sha256;
    }

    public CondaComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("build")]
    public string Build { get; set; }

    [JsonPropertyName("channel")]
    public string Channel { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; }

    [JsonPropertyName("subdir")]
    public string Subdir { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("mD5")]
    public string MD5 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Conda;

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} {this.Build} {this.Channel} {this.Subdir} {this.Namespace} {this.Url} {this.MD5} {this.Sha256} - {this.Type}";
}
