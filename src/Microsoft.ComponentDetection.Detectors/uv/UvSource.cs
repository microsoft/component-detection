namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Runtime.Serialization;

[DataContract]
internal class UvSource
{
    [DataMember(Name = "registry")]
    public string? Registry { get; set; }

    [DataMember(Name = "virtual")]
    public string? Virtual { get; set; }

    [DataMember(Name = "git")]
    public string? Git { get; set; }
}
