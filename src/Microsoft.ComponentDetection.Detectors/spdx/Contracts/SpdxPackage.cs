namespace Microsoft.ComponentDetection.Detectors.Spdx.Contracts;

using System.Collections.Generic;
using Newtonsoft.Json;

public class SpdxPackage
{
    [JsonProperty("copyrightText")]
    public string CopyrightText { get; set; }

    [JsonProperty("downloadLocation")]
    public string DownloadLocation { get; set; }

    [JsonProperty("externalRefs")]
    public IEnumerable<SpdxExternalRefs> ExternalRefs { get; set; }

    [JsonProperty("licenseConcluded")]
    public string LicenseConcluded { get; set; }

    [JsonProperty("licenseDeclared")]
    public string LicenseDeclared { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty(nameof(SPDXID))]
    public string SPDXID { get; set; }

    [JsonProperty("supplier")]
    public string Supplier { get; set; }

    [JsonProperty("versionInfo")]
    public string Version { get; set; }
}
