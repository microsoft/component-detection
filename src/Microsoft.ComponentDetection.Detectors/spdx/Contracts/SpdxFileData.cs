namespace Microsoft.ComponentDetection.Detectors.Spdx.Contracts;

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class SpdxFileData
{
    [JsonProperty("creationInfo")]
    public CreationInfo CreationInfo { get; set; }

    [JsonProperty("dataLicense")]
    public string DataLicense { get; set; }

    [JsonProperty("documentDescribes")]
    public IEnumerable<string> DocumentDescribes { get; set; }

    [JsonProperty("documentNamespace")]
    public string DocumentNamespace { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("packages")]
    public IEnumerable<SpdxPackage> Packages { get; set; }

    [JsonProperty(nameof(SPDXID))]
    public string SPDXID { get; set; }

    [JsonProperty("spdxVersion")]
    public string Version { get; set; }

    internal bool HasPackages() => this.Packages?.Count() > 0;
}
