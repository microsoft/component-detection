#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Represents the yarn.lock file for yarn v2+ (berry).
/// There is no official documentation for the format of the yarn.lock file.
/// This is based on the source code of https://github.com/yarnpkg/berry/blob/master/packages/yarnpkg-core/sources/Project.ts.
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
