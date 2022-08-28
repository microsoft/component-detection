namespace Microsoft.ComponentDetection.Detectors.Yarn
{
    using System.Collections.Generic;

    public class YarnLockFile
    {
        /// <summary>
        /// Gets or sets the declared Yarn Lock Version.
        /// </summary>
        public YarnLockVersion LockVersion { get; set; }

        /// <summary>
        /// Gets or sets the component entries.
        /// </summary>
        public IEnumerable<YarnEntry> Entries { get; set; }
    }
}
