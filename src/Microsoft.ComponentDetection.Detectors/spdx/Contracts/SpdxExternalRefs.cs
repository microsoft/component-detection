namespace Microsoft.ComponentDetection.Detectors.Spdx.Contracts;

using Newtonsoft.Json;

public class SpdxExternalRefs
{
    [JsonProperty("referenceCategory")]
    public string Category { get; set; }

    [JsonProperty("referenceLocator")]
    public string Locator { get; set; }

    [JsonProperty("referenceType")]
    public string Type { get; set; }
}
