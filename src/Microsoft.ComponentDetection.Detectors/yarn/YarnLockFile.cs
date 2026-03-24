#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.Collections.Generic;

public class YarnLockFile
{
    /// <summary>
    /// Gets or sets the declared Yarn Lock Version.
    /// </summary>
    public YarnLockVersion LockVersion { get; set; }

    /// <summary>
    /// The explicit lockfile version from the `metadata` section of the lock file. 1 if not present.
    /// </summary>
    public string LockfileVersion { get; set; } = "1";

    /// <summary>
    /// Gets or sets the component entries.
    /// </summary>
    public IEnumerable<YarnEntry> Entries { get; set; }
}
