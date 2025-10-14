#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Conan.Contracts;

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class ConanLockNode
{
    [JsonPropertyName("context")]
    public string Context { get; set; }

    [JsonPropertyName("modified")]
    public bool? Modified { get; set; }

    [JsonPropertyName("options")]
    public string Options { get; set; }

    [JsonPropertyName("package_id")]
    public string PackageId { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("prev")]
    public string Previous { get; set; }

    [JsonPropertyName("ref")]
    public string Reference { get; set; }

    [JsonPropertyName("requires")]
    public string[] Requires { get; set; }

    [JsonPropertyName("build_requires")]
    public string[] BuildRequires { get; set; }

    internal string Name() => this.Reference == null ? string.Empty : this.Reference.Split('/', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault("Unknown");

    internal TypedComponent ToComponent() => new ConanComponent(this.Name(), this.Version(), this.Previous, this.PackageId);

    internal string Version() => this.Reference == null ? string.Empty : this.Reference.Split('/', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).FirstOrDefault("None");
}
