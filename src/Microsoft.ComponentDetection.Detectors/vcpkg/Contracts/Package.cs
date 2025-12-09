#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System.Text.Json.Serialization;

public class Package
{
    [JsonPropertyName("SPDXID")]
    public string SPDXID { get; set; }

    [JsonPropertyName("versionInfo")]
    public string VersionInfo { get; set; }

    [JsonPropertyName("downloadLocation")]
    public string DownloadLocation { get; set; }

    [JsonPropertyName("packageFileName")]
    public string Filename { get; set; }

    [JsonPropertyName("homepage")]
    public string Homepage { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("annotations")]
    public Annotation[] Annotations { get; set; }
}
