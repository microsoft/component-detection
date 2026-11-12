namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerPlatformInventory
{
    [JsonPropertyName("platform")]
    public IEnumerable<ComposerPlatformPackage?>? Packages { get; set; }
}
