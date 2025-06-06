namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;

public class UvComponent : TypedComponent
{
    public UvComponent() => this.Metadata = [];

    public UvComponent(string name, string version, Dictionary<string, object> metadata = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Pip));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Pip));
        this.Metadata = metadata ?? [];
    }

    public string Name { get; set; }

    public string Version { get; set; }

    /// <summary>
    /// Arbitrary metadata from the uv.lock TOML package entry (e.g., dependencies, source, wheels, sdist, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; }

    public override ComponentType Type => ComponentType.Uv;

    protected override string ComputeId()
    {
        return $"{this.Name}:{this.Version}";
    }
}
