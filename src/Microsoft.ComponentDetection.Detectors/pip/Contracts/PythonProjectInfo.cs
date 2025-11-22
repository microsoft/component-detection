#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

public class PythonProjectInfo
{
    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonProperty("author_email")]
    [JsonPropertyName("author_email")]
    public string AuthorEmail { get; set; }

    [JsonPropertyName("classifiers")]
    public List<string> Classifiers { get; set; }

    [JsonPropertyName("license")]
    public string License { get; set; }

    [JsonPropertyName("maintainer")]
    public string Maintainer { get; set; }

    [JsonProperty("maintainer_email")]
    [JsonPropertyName("maintainer_email")]
    public string MaintainerEmail { get; set; }

    // Add other properties from the "info" object as needed
}
