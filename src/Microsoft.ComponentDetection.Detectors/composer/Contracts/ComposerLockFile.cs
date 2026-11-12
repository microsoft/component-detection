namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerLockFile
{
    [JsonPropertyName("packages")]
    public IReadOnlyList<ComposerPackage?>? Packages { get; set; }

    [JsonPropertyName("packages-dev")]
    public IReadOnlyList<ComposerPackage?>? DevelopmentPackages { get; set; }

    [JsonPropertyName("aliases")]
    public IReadOnlyList<ComposerAlias?>? Aliases { get; set; }
}
