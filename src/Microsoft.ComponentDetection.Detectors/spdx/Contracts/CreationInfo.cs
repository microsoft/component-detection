namespace Microsoft.ComponentDetection.Detectors.Spdx.Contracts;

using System.Collections.Generic;
using Newtonsoft.Json;

public class CreationInfo
{
    [JsonProperty("created")]
    public string Created { get; set; }

    [JsonProperty("creators")]
    public IEnumerable<string> Creators { get; set; }
}
