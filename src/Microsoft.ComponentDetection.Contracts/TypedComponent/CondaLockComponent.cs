namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;

public class CondaLockComponent : TypedComponent
{
    public CondaLockComponent(string name, string version, string category, Dictionary<string, string> dependencies, Dictionary<string, string> hash, string manager, bool optional, string platform, string url)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conda));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conda));
        this.Category = category;
        this.Dependencies = dependencies;
        this.Hash = hash;
        this.Manager = manager;
        this.Optional = optional;
        this.Platform = platform;
        this.Url = url;
        this.Version = version;
    }

    private CondaLockComponent()
    {
        /* Reserved for deserialization */
    }

    public string Category { get; set; }

    public Dictionary<string, string> Dependencies { get; set; }

    public Dictionary<string, string> Hash { get; set; }

    public string Manager { get; set; }

    public string Name { get; set; }

    public bool Optional { get; set; }

    public string Platform { get; set; }

    public string Url { get; set; }

    public string Version { get; set; }

    public override ComponentType Type => ComponentType.Conda;

    public override string Id => $"{this.Name} {this.Version} {this.Manager} {this.Platform} {this.Url} {this.Category} - {this.Type}";
}
