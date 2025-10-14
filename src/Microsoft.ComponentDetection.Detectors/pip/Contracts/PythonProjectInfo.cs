#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using Newtonsoft.Json;

public class PythonProjectInfo
{
    public string Author { get; set; }

    [JsonProperty("author_email")]
    public string AuthorEmail { get; set; }

    public List<string> Classifiers { get; set; }

    public string License { get; set; }

    public string Maintainer { get; set; }

    [JsonProperty("maintainer_email")]
    public string MaintainerEmail { get; set; }

    // Add other properties from the "info" object as needed
}
