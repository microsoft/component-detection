#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System;
using System.Text.Json.Serialization;

public class Annotation
{
    [JsonPropertyName("annotationDate")]
    public DateTime Date { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; }

    [JsonPropertyName("annotationType")]
    public string Type { get; set; }

    [JsonPropertyName("annotator")]
    public string Annotator { get; set; }
}
