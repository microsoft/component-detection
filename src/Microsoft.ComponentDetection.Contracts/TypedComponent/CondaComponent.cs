#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class CondaComponent : TypedComponent
{
    public CondaComponent(string name, string version, string build, string channel, string subdir, string @namespace, string url, string md5)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conda));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conda));
        this.Build = build;
        this.Channel = channel;
        this.Subdir = subdir;
        this.Namespace = @namespace;
        this.Url = url;
        this.MD5 = md5;
    }

    private CondaComponent()
    {
        /* Reserved for deserialization */
    }

    public string Build { get; set; }

    public string Channel { get; set; }

    public string Name { get; set; }

    public string Namespace { get; set; }

    public string Subdir { get; set; }

    public string Version { get; set; }

    public string Url { get; set; }

    public string MD5 { get; set; }

    public override ComponentType Type => ComponentType.Conda;

    protected override string ComputeId() => $"{this.Name} {this.Version} {this.Build} {this.Channel} {this.Subdir} {this.Namespace} {this.Url} {this.MD5} - {this.Type}";
}
