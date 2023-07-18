namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Represents the yarn.lock file for yarn v2+.
/// </summary>
public sealed record YarnBerryLockfile
{
    /// <summary>
    /// Gets the metadata of the yarn.lock file.
    /// </summary>
    [YamlMember(Alias = "__metadata")]
    public YarnBerryLockfileMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the entries of the yarn.lock file.
    /// </summary>
    public IDictionary<string, YarnBerryLockfileEntry> Entries { get; init; }
}
