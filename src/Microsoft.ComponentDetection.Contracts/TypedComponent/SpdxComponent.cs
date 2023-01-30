﻿namespace Microsoft.ComponentDetection.Contracts.TypedComponent;
using System;

public class SpdxComponent : TypedComponent
{
    private SpdxComponent()
    {
        /* Reserved for deserialization */
    }

    public SpdxComponent(string spdxVersion, Uri documentNamespace, string name, string checksum, string rootElementId, string path)
    {
        this.SpdxVersion = ValidateRequiredInput(spdxVersion, nameof(this.SpdxVersion), nameof(ComponentType.Spdx));
        this.DocumentNamespace = ValidateRequiredInput(documentNamespace, nameof(this.DocumentNamespace), nameof(ComponentType.Spdx));
        this.Name = ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Spdx));
        this.Checksum = ValidateRequiredInput(checksum, nameof(this.Checksum), nameof(ComponentType.Spdx));
        this.RootElementId = ValidateRequiredInput(rootElementId, nameof(this.RootElementId), nameof(ComponentType.Spdx));
        this.Path = ValidateRequiredInput(path, nameof(this.Path), nameof(ComponentType.Spdx));
    }

    public override ComponentType Type => ComponentType.Spdx;

    public string RootElementId { get; set; }

    public string Name { get; set; }

    public string SpdxVersion { get; set; }

    public Uri DocumentNamespace { get; set; }

    public string Checksum { get; set; }

    public string Path { get; set; }

    public override string Id => $"{this.Name}-{this.SpdxVersion}-{this.Checksum}";
}
