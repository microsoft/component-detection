namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Runtime.Serialization;

[DataContract]
internal class UvDependency
{
    [DataMember(Name = "name")]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "specifier")]
    public string? Specifier { get; set; }
}
