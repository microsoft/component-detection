#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;

public class SpdxComponent : TypedComponent
{
    private SpdxComponent()
    {
        /* Reserved for deserialization */
    }

    public SpdxComponent(string spdxVersion, Uri documentNamespace, string name, string checksum, string rootElementId, string path)
    {
        this.SpdxVersion = this.ValidateRequiredInput(spdxVersion, nameof(this.SpdxVersion), nameof(ComponentType.Spdx));
        this.DocumentNamespace = this.ValidateRequiredInput(documentNamespace, nameof(this.DocumentNamespace), nameof(ComponentType.Spdx));
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Spdx));
        this.Checksum = this.ValidateRequiredInput(checksum, nameof(this.Checksum), nameof(ComponentType.Spdx));
        this.RootElementId = this.ValidateRequiredInput(rootElementId, nameof(this.RootElementId), nameof(ComponentType.Spdx));
        this.Path = this.ValidateRequiredInput(path, nameof(this.Path), nameof(ComponentType.Spdx));
    }

    public override ComponentType Type => ComponentType.Spdx;

    public string RootElementId { get; set; }

    public string Name { get; set; }

    public string SpdxVersion { get; set; }

    public Uri DocumentNamespace { get; set; }

    public string Checksum { get; set; }

    public string Path { get; set; }

    protected override string ComputeId() => $"{this.Name}-{this.SpdxVersion}-{this.Checksum}";
}
