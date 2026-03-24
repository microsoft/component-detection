#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

using System.Collections.Generic;

public interface IYarnBlockFile : IEnumerable<YarnBlock>
{
    YarnLockVersion YarnLockVersion { get; set; }

    /// <summary>
    /// The explicit version extracted from the `metadata` section of yarn lock files of <see cref="YarnLockVersion.Berry"/>.
    /// </summary>
    string LockfileVersion { get; set; }
}
