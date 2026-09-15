#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class PythonProjectInfo
{
    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("author_email")]
    public string AuthorEmail { get; set; }

    [JsonPropertyName("classifiers")]
    public List<string> Classifiers { get; set; }

    [JsonPropertyName("license")]
    public string License { get; set; }

    [JsonPropertyName("maintainer")]
    public string Maintainer { get; set; }

    [JsonPropertyName("maintainer_email")]
    public string MaintainerEmail { get; set; }

    // Add other properties from the "info" object as needed
}
