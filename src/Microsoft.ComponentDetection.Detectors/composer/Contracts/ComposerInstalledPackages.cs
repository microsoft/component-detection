namespace Microsoft.ComponentDetection.Detectors.Composer.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal class ComposerInstalledPackages
{
    [JsonPropertyName("packages")]
    public IReadOnlyList<ComposerPackage?>? Packages { get; set; }

    [JsonPropertyName("dev-package-names")]
    public IEnumerable<string?>? DevelopmentPackageNames { get; set; }
}
