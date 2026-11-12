namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerExtra
{
    [JsonPropertyName("branch-alias")]
    public Dictionary<string, string?>? BranchAliases { get; set; }
}
